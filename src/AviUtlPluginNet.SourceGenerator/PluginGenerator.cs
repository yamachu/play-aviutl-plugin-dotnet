using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AviUtlPluginNet.SourceGenerator
{
    /// <summary>
    /// [AviUtl2Plugin] が付与されたクラスの実装インターフェースから
    /// プラグイン種別(Input/Output)・能力(capability)・機能(feature)を推論し、
    /// ネイティブエクスポート({Get*PluginTable} / Initialize* 等)を生成するジェネレータ
    /// </summary>
    [Generator]
    public class AviUtlPluginSourceGenerator : IIncrementalGenerator
    {
        private const string AbstractionsNamespace = "AviUtlPluginNet.Abstractions";
        private const string AttributeFullName = "AviUtlPluginNet.Abstractions.AviUtl2PluginAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var pluginClasses = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeFullName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => CreateModel(ctx))
                .Where(static m => m != null)
                .Collect();

            context.RegisterSourceOutput(pluginClasses, Execute);
        }

        private static PluginModel? CreateModel(GeneratorAttributeSyntaxContext context)
        {
            if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
            {
                return null;
            }

            var model = new PluginModel
            {
                ClassName = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns
                    ? ns.ToDisplayString()
                    : "",
                Location = classSymbol.Locations.FirstOrDefault() ?? Location.None,
            };

            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (iface.ContainingNamespace?.ToDisplayString() != AbstractionsNamespace)
                {
                    continue;
                }

                switch (iface.Name)
                {
                    // 種別
                    case "IInputPlugin" when iface.TypeArguments.Length == 1:
                        model.IsInput = true;
                        model.HandleType = iface.TypeArguments[0]
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        break;
                    case "IOutputPlugin":
                        model.IsOutput = true;
                        break;
                    case "IScriptModulePlugin":
                        model.IsScriptModule = true;
                        break;
                    case "ICommonPlugin":
                        model.IsCommon = true;
                        break;

                    // Input能力
                    case "IInputVideo" when iface.TypeArguments.Length == 1:
                        model.HasInputVideo = true;
                        break;
                    case "IInputAudio" when iface.TypeArguments.Length == 1:
                        model.HasInputAudio = true;
                        break;
                    case "IInputConcurrent":
                        model.HasInputConcurrent = true;
                        break;
                    case "IInputMultiTrack" when iface.TypeArguments.Length == 1:
                        model.HasInputMultiTrack = true;
                        break;
                    case "IInputTimeToFrame" when iface.TypeArguments.Length == 1:
                        model.HasInputTimeToFrame = true;
                        break;
                    case "IInputConfigDialog":
                        model.HasInputConfigDialog = true;
                        break;

                    // Output能力
                    case "IOutputVideo":
                        model.HasOutputVideo = true;
                        break;
                    case "IOutputAudio":
                        model.HasOutputAudio = true;
                        break;
                    case "IOutputImageOnly":
                        model.HasOutputImageOnly = true;
                        break;
                    case "IOutputConfigDialog":
                        model.HasOutputConfigDialog = true;
                        break;
                    case "IOutputConfigText":
                        model.HasOutputConfigText = true;
                        break;
                    case "IOutputProjectConfig":
                        model.HasOutputProjectConfig = true;
                        break;

                    // ホストサービス機能
                    case "IUseLogger":
                        model.HasLogger = true;
                        break;
                    case "IUseConfig":
                        model.HasConfig = true;
                        break;
                    case "IUseCache":
                        model.HasCache = true;
                        break;
                    case "IPluginLifecycle":
                        model.HasLifecycle = true;
                        break;
                    case "IRequireVersion":
                        model.HasRequiredVersion = true;
                        break;
                }
            }

            CollectScriptFunctions(classSymbol, model);

            return model;
        }

        /// <summary>
        /// [ScriptFunction] が付与されたメソッドを収集する
        /// 有効なシグネチャ: private/protectedでない void Method(ScriptModuleContext)
        /// </summary>
        private static void CollectScriptFunctions(INamedTypeSymbol classSymbol, PluginModel model)
        {
            foreach (var method in classSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                var attribute = method.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass?.Name == "ScriptFunctionAttribute" &&
                    a.AttributeClass.ContainingNamespace?.ToDisplayString() == AbstractionsNamespace);
                if (attribute == null)
                {
                    continue;
                }

                var isValidSignature =
                    method.ReturnsVoid &&
                    !method.IsStatic &&
                    method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal &&
                    method.Parameters.Length == 1 &&
                    method.Parameters[0].Type.Name == "ScriptModuleContext" &&
                    method.Parameters[0].Type.ContainingNamespace?.ToDisplayString() == AbstractionsNamespace;
                if (!isValidSignature)
                {
                    model.InvalidScriptFunctions.Add(method.Name);
                    continue;
                }

                var scriptName = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? method.Name;
                model.ScriptFunctions.Add(new ScriptFunctionInfo(method.Name, scriptName));
            }
        }

        private static void Execute(SourceProductionContext context, ImmutableArray<PluginModel?> pluginClasses)
        {
            var models = pluginClasses.Where(m => m != null).Cast<PluginModel>().ToList();

            // NativeAOTのエクスポート名はアセンブリ内で一意のため、1アセンブリ1プラグインのみ
            if (models.Count > 1)
            {
                foreach (var m in models)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MultiplePluginClasses, m.Location, m.ClassName));
                }
                return;
            }

            foreach (var model in models)
            {
                if (!Validate(context, model))
                {
                    continue;
                }

                var source = model.IsInput ? GenerateInputNativeLibrary(model)
                    : model.IsOutput ? GenerateOutputNativeLibrary(model)
                    : model.IsScriptModule ? GenerateScriptModuleNativeLibrary(model)
                    : GenerateCommonNativeLibrary(model);
                context.AddSource($"{model.ClassName}NativeLibrary.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private static bool Validate(SourceProductionContext context, PluginModel model)
        {
            var kindCount = (model.IsInput ? 1 : 0) + (model.IsOutput ? 1 : 0) + (model.IsScriptModule ? 1 : 0) + (model.IsCommon ? 1 : 0);

            if (kindCount == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoPluginKind, model.Location, model.ClassName));
                return false;
            }

            if (kindCount > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MultiplePluginKinds, model.Location, model.ClassName));
                return false;
            }

            if (model.IsInput && !model.HasInputVideo && !model.HasInputAudio)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InputWithoutMedia, model.Location, model.ClassName));
                return false;
            }

            if (model.IsOutput && !model.HasOutputVideo && !model.HasOutputAudio)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.OutputWithoutMedia, model.Location, model.ClassName));
                return false;
            }

            if (model.HasOutputImageOnly && !model.HasOutputVideo)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ImageOnlyWithoutVideo, model.Location, model.ClassName));
                return false;
            }

            if (model.InvalidScriptFunctions.Count > 0)
            {
                foreach (var method in model.InvalidScriptFunctions)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidScriptFunction, model.Location, model.ClassName, method));
                }
                return false;
            }

            if (model.IsScriptModule && model.ScriptFunctions.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ScriptModuleWithoutFunctions, model.Location, model.ClassName));
                return false;
            }

            return true;
        }

        private static string GenerateInputNativeLibrary(PluginModel model)
        {
            var cls = model.FullClassName;
            var handle = model.HandleType!;
            var kindInterface = $"global::{AbstractionsNamespace}.IInputPlugin<{handle}>";

            var flags = new List<string>();
            if (model.HasInputVideo) flags.Add("InputPluginTableFlag.Video");
            if (model.HasInputAudio) flags.Add("InputPluginTableFlag.Audio");
            if (model.HasInputConcurrent) flags.Add("InputPluginTableFlag.Concurrent");
            if (model.HasInputMultiTrack) flags.Add("InputPluginTableFlag.MultiTrack");
            var flagExpression = flags.Count > 0 ? string.Join(" | ", flags) : "InputPluginTableFlag.None";

            var builder = new StringBuilder();
            builder.Append($@"// <auto-generated/>
#nullable enable
using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.AUI2;

{(model.Namespace.Length > 0 ? $"namespace {model.Namespace};" : "")}

public static unsafe class {model.ClassName}NativeLibrary
{{
    private static readonly {cls} plugin = new {cls}();
    private static IntPtr pluginTablePtr;

    [UnmanagedCallersOnly(EntryPoint = ""GetInputPluginTable"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static IntPtr GetInputPluginTable()
    {{
        if (pluginTablePtr == IntPtr.Zero)
        {{
            var table = (INPUT_PLUGIN_TABLE*)Marshal.AllocHGlobal(sizeof(INPUT_PLUGIN_TABLE));
            table->flag = {flagExpression};
            table->name = Marshal.StringToHGlobalUni({cls}.Name);
            table->filefilter = Marshal.StringToHGlobalUni({cls}.FileFilter);
            table->information = Marshal.StringToHGlobalUni({cls}.Information);
            table->func_open = &FuncOpen;
            table->func_close = &FuncClose;
            table->func_info_get = &FuncInfoGet;
            table->func_read_video = {(model.HasInputVideo ? "&FuncReadVideo" : "null")};
            table->func_read_audio = {(model.HasInputAudio ? "&FuncReadAudio" : "null")};
            table->func_config = {(model.HasInputConfigDialog ? "&FuncConfig" : "null")};
            table->func_set_track = {(model.HasInputMultiTrack ? "&FuncSetTrack" : "null")};
            table->func_time_to_frame = {(model.HasInputTimeToFrame ? "&FuncTimeToFrame" : "null")};
            pluginTablePtr = (IntPtr)table;
        }}
        return pluginTablePtr;
    }}

    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static IntPtr FuncOpen(IntPtr file)
    {{
        try
        {{
            string? path = Marshal.PtrToStringUni(file);
            if (string.IsNullOrEmpty(path))
            {{
                return IntPtr.Zero;
            }}
            var handle = (({kindInterface})plugin).Open(path);
            if (handle == null)
            {{
                return IntPtr.Zero;
            }}
            return (IntPtr)GCHandle.Alloc(handle);
        }}
        catch
        {{
            return IntPtr.Zero;
        }}
    }}

    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncClose(IntPtr ih)
    {{
        if (ih == IntPtr.Zero) return false;
        try
        {{
            var gch = (GCHandle)ih;
            if (gch.Target is not {handle} handle) return false;
            var result = (({kindInterface})plugin).Close(handle);
            gch.Free();
            return result;
        }}
        catch
        {{
            return false;
        }}
    }}

    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncInfoGet(IntPtr ih, IntPtr iip)
    {{
        if (ih == IntPtr.Zero || iip == IntPtr.Zero) return false;
        try
        {{
            if (((GCHandle)ih).Target is not {handle} handle) return false;
            if (!(({kindInterface})plugin).TryGetInfo(handle, out var info)) return false;
            *(INPUT_INFO*)iip = info;
            return true;
        }}
        catch
        {{
            return false;
        }}
    }}
");

            if (model.HasInputVideo)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static int FuncReadVideo(IntPtr ih, int frame, IntPtr buf)
    {{
        if (ih == IntPtr.Zero || buf == IntPtr.Zero) return 0;
        try
        {{
            if (((GCHandle)ih).Target is not {handle} handle) return 0;
            var data = ((global::{AbstractionsNamespace}.IInputVideo<{handle}>)plugin).ReadVideo(handle, frame);
            if (data.IsEmpty) return 0;
            fixed (byte* src = data)
            {{
                Buffer.MemoryCopy(src, (void*)buf, data.Length, data.Length);
            }}
            return data.Length;
        }}
        catch
        {{
            return 0;
        }}
    }}
");
            }

            if (model.HasInputAudio)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static int FuncReadAudio(IntPtr ih, int start, int length, IntPtr buf)
    {{
        if (ih == IntPtr.Zero || buf == IntPtr.Zero) return 0;
        try
        {{
            if (((GCHandle)ih).Target is not {handle} handle) return 0;
            var data = ((global::{AbstractionsNamespace}.IInputAudio<{handle}>)plugin).ReadAudio(handle, start, length);
            if (data.IsEmpty) return 0;
            fixed (byte* src = data)
            {{
                Buffer.MemoryCopy(src, (void*)buf, data.Length, data.Length);
            }}
            return data.Length;
        }}
        catch
        {{
            return 0;
        }}
    }}
");
            }

            if (model.HasInputConfigDialog)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncConfig(IntPtr hwnd, IntPtr dllHInstance)
    {{
        try
        {{
            return ((global::{AbstractionsNamespace}.IInputConfigDialog)plugin).Config(hwnd, dllHInstance);
        }}
        catch
        {{
            return false;
        }}
    }}
");
            }

            if (model.HasInputMultiTrack)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static int FuncSetTrack(IntPtr ih, int type, int index)
    {{
        if (ih == IntPtr.Zero) return -1;
        try
        {{
            if (((GCHandle)ih).Target is not {handle} handle) return -1;
            return ((global::{AbstractionsNamespace}.IInputMultiTrack<{handle}>)plugin).SetTrack(handle, (TrackType)type, index);
        }}
        catch
        {{
            return -1;
        }}
    }}
");
            }

            if (model.HasInputTimeToFrame)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static int FuncTimeToFrame(IntPtr ih, double time)
    {{
        if (ih == IntPtr.Zero) return 0;
        try
        {{
            if (((GCHandle)ih).Target is not {handle} handle) return 0;
            return ((global::{AbstractionsNamespace}.IInputTimeToFrame<{handle}>)plugin).TimeToFrame(handle, time);
        }}
        catch
        {{
            return 0;
        }}
    }}
");
            }

            AppendFeatureExports(builder, model);

            builder.Append(@"}
");
            return builder.ToString();
        }

        private static string GenerateOutputNativeLibrary(PluginModel model)
        {
            var cls = model.FullClassName;

            var flags = new List<string>();
            if (model.HasOutputVideo) flags.Add("OutputPluginTableFlag.Video");
            if (model.HasOutputAudio) flags.Add("OutputPluginTableFlag.Audio");
            if (model.HasOutputImageOnly) flags.Add("OutputPluginTableFlag.Image");
            if (model.HasOutputProjectConfig) flags.Add("OutputPluginTableFlag.ProjectConfig");
            var flagExpression = flags.Count > 0 ? string.Join(" | ", flags) : "OutputPluginTableFlag.None";

            var builder = new StringBuilder();
            builder.Append($@"// <auto-generated/>
#nullable enable
using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.AUO2;

{(model.Namespace.Length > 0 ? $"namespace {model.Namespace};" : "")}

public static unsafe class {model.ClassName}NativeLibrary
{{
    private static readonly {cls} plugin = new {cls}();
    private static IntPtr pluginTablePtr;

    [UnmanagedCallersOnly(EntryPoint = ""GetOutputPluginTable"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static IntPtr GetOutputPluginTable()
    {{
        if (pluginTablePtr == IntPtr.Zero)
        {{
            var table = (OUTPUT_PLUGIN_TABLE*)Marshal.AllocHGlobal(sizeof(OUTPUT_PLUGIN_TABLE));
            table->flag = {flagExpression};
            table->name = Marshal.StringToHGlobalUni({cls}.Name);
            table->filefilter = Marshal.StringToHGlobalUni({cls}.FileFilter);
            table->information = Marshal.StringToHGlobalUni({cls}.Information);
            table->func_output = &FuncOutput;
            table->func_config = {(model.HasOutputConfigDialog ? "&FuncConfig" : "null")};
            table->func_get_config_text = {(model.HasOutputConfigText ? "&FuncGetConfigText" : "null")};
            table->func_load_project_config = {(model.HasOutputProjectConfig ? "&FuncLoadProjectConfig" : "null")};
            table->func_save_project_config = {(model.HasOutputProjectConfig ? "&FuncSaveProjectConfig" : "null")};
            pluginTablePtr = (IntPtr)table;
        }}
        return pluginTablePtr;
    }}

    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncOutput(IntPtr oip)
    {{
        if (oip == IntPtr.Zero) return false;
        try
        {{
            return ((global::{AbstractionsNamespace}.IOutputPlugin)plugin).Output(new global::{AbstractionsNamespace}.OutputContext(oip));
        }}
        catch
        {{
            return false;
        }}
    }}
");

            if (model.HasOutputConfigDialog)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncConfig(IntPtr hwnd, IntPtr dllHInstance)
    {{
        try
        {{
            return ((global::{AbstractionsNamespace}.IOutputConfigDialog)plugin).Config(hwnd, dllHInstance);
        }}
        catch
        {{
            return false;
        }}
    }}
");
            }

            if (model.HasOutputConfigText)
            {
                builder.Append($@"
    private static IntPtr configTextPtr;

    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static IntPtr FuncGetConfigText()
    {{
        try
        {{
            var text = ((global::{AbstractionsNamespace}.IOutputConfigText)plugin).GetConfigText();
            var newPtr = Marshal.StringToHGlobalUni(text);
            var oldPtr = configTextPtr;
            configTextPtr = newPtr;
            if (oldPtr != IntPtr.Zero)
            {{
                Marshal.FreeHGlobal(oldPtr);
            }}
            return newPtr;
        }}
        catch
        {{
            return IntPtr.Zero;
        }}
    }}
");
            }

            if (model.HasOutputProjectConfig)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncLoadProjectConfig(IntPtr project)
    {{
        try
        {{
            return ((global::{AbstractionsNamespace}.IOutputProjectConfig)plugin).LoadProjectConfig(new global::{AbstractionsNamespace}.ProjectFile(project));
        }}
        catch
        {{
            return false;
        }}
    }}

    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncSaveProjectConfig(IntPtr project)
    {{
        try
        {{
            return ((global::{AbstractionsNamespace}.IOutputProjectConfig)plugin).SaveProjectConfig(new global::{AbstractionsNamespace}.ProjectFile(project));
        }}
        catch
        {{
            return false;
        }}
    }}
");
            }

            AppendFeatureExports(builder, model);

            builder.Append(@"}
");
            return builder.ToString();
        }

        private static string GenerateScriptModuleNativeLibrary(PluginModel model)
        {
            var cls = model.FullClassName;
            var functionCount = model.ScriptFunctions.Count;

            var builder = new StringBuilder();
            builder.Append($@"// <auto-generated/>
#nullable enable
using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.Module2;

{(model.Namespace.Length > 0 ? $"namespace {model.Namespace};" : "")}

public static unsafe class {model.ClassName}NativeLibrary
{{
    private static readonly {cls} plugin = new {cls}();
    private static IntPtr pluginTablePtr;

    [UnmanagedCallersOnly(EntryPoint = ""GetScriptModuleTable"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static IntPtr GetScriptModuleTable()
    {{
        if (pluginTablePtr == IntPtr.Zero)
        {{
            // 関数名がnullの要素で終端するリスト
            var functions = (SCRIPT_MODULE_FUNCTION*)Marshal.AllocHGlobal(sizeof(SCRIPT_MODULE_FUNCTION) * {functionCount + 1});
");

            for (var i = 0; i < functionCount; i++)
            {
                var function = model.ScriptFunctions[i];
                builder.Append($@"            functions[{i}].name = Marshal.StringToHGlobalUni(""{EscapeLiteral(function.ScriptName)}"");
            functions[{i}].func = &ScriptFunc_{function.MethodName};
");
            }

            builder.Append($@"            functions[{functionCount}].name = IntPtr.Zero;
            functions[{functionCount}].func = null;

            var table = (SCRIPT_MODULE_TABLE*)Marshal.AllocHGlobal(sizeof(SCRIPT_MODULE_TABLE));
            table->information = Marshal.StringToHGlobalUni({cls}.Information);
            table->functions = functions;
            pluginTablePtr = (IntPtr)table;
        }}
        return pluginTablePtr;
    }}
");

            foreach (var function in model.ScriptFunctions)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static void ScriptFunc_{function.MethodName}(IntPtr param)
    {{
        var context = new global::{AbstractionsNamespace}.ScriptModuleContext(param);
        try
        {{
            plugin.{function.MethodName}(context);
        }}
        catch (Exception ex)
        {{
            try
            {{
                context.SetError(ex.Message);
            }}
            catch
            {{
            }}
        }}
    }}
");
            }

            AppendFeatureExports(builder, model);

            builder.Append(@"}
");
            return builder.ToString();
        }

        private static string GenerateCommonNativeLibrary(PluginModel model)
        {
            var cls = model.FullClassName;

            var builder = new StringBuilder();
            builder.Append($@"// <auto-generated/>
#nullable enable
using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.Plugin2;

{(model.Namespace.Length > 0 ? $"namespace {model.Namespace};" : "")}

public static unsafe class {model.ClassName}NativeLibrary
{{
    private static readonly {cls} plugin = new {cls}();
    private static IntPtr pluginTablePtr;

    [UnmanagedCallersOnly(EntryPoint = ""GetCommonPluginTable"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static IntPtr GetCommonPluginTable()
    {{
        if (pluginTablePtr == IntPtr.Zero)
        {{
            var table = (COMMON_PLUGIN_TABLE*)Marshal.AllocHGlobal(sizeof(COMMON_PLUGIN_TABLE));
            table->name = Marshal.StringToHGlobalUni({cls}.Name);
            table->information = Marshal.StringToHGlobalUni({cls}.Information);
            pluginTablePtr = (IntPtr)table;
        }}
        return pluginTablePtr;
    }}

    [UnmanagedCallersOnly(EntryPoint = ""RegisterPlugin"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static void RegisterPlugin(IntPtr host)
    {{
        if (host == IntPtr.Zero) return;
        try
        {{
            ((global::{AbstractionsNamespace}.ICommonPlugin)plugin).Register(new global::{AbstractionsNamespace}.NativeHostApp(host));
        }}
        catch
        {{
        }}
    }}
");

            AppendFeatureExports(builder, model);

            builder.Append(@"}
");
            return builder.ToString();
        }

        private static string EscapeLiteral(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>
        /// ホストサービス機能(cake機能)の任意エクスポートを生成します
        /// 新しい機能(例: cache2のIUseCache)はここにエクスポートを追加するだけで対応できます
        /// </summary>
        private static void AppendFeatureExports(StringBuilder builder, PluginModel model)
        {
            if (model.HasLogger)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(EntryPoint = ""InitializeLogger"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static void InitializeLogger(IntPtr logger)
    {{
        if (logger == IntPtr.Zero) return;
        try
        {{
            ((global::{AbstractionsNamespace}.IUseLogger)plugin).AttachLogger(new global::{AbstractionsNamespace}.NativeLogger2(logger));
        }}
        catch
        {{
        }}
    }}
");
            }

            if (model.HasConfig)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(EntryPoint = ""InitializeConfig"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static void InitializeConfig(IntPtr config)
    {{
        if (config == IntPtr.Zero) return;
        try
        {{
            ((global::{AbstractionsNamespace}.IUseConfig)plugin).AttachConfig(new global::{AbstractionsNamespace}.NativeConfig2(config));
        }}
        catch
        {{
        }}
    }}
");
            }

            if (model.HasCache)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(EntryPoint = ""InitializeCache"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static void InitializeCache(IntPtr cache)
    {{
        if (cache == IntPtr.Zero) return;
        try
        {{
            ((global::{AbstractionsNamespace}.IUseCache)plugin).AttachCache(new global::{AbstractionsNamespace}.NativeCache2(cache));
        }}
        catch
        {{
        }}
    }}
");
            }

            if (model.HasLifecycle)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(EntryPoint = ""InitializePlugin"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static bool InitializePlugin(uint version)
    {{
        try
        {{
            return ((global::{AbstractionsNamespace}.IPluginLifecycle)plugin).OnInitialize(version);
        }}
        catch
        {{
            return false;
        }}
    }}

    [UnmanagedCallersOnly(EntryPoint = ""UninitializePlugin"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static void UninitializePlugin()
    {{
        try
        {{
            ((global::{AbstractionsNamespace}.IPluginLifecycle)plugin).OnUninitialize();
        }}
        catch
        {{
        }}
    }}
");
            }

            if (model.HasRequiredVersion)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(EntryPoint = ""RequiredVersion"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static uint RequiredVersion()
    {{
        return {model.FullClassName}.RequiredVersion;
    }}
");
            }
        }
    }

    internal static class Diagnostics
    {
        private const string Category = "AviUtlPluginNet";

        public static readonly DiagnosticDescriptor NoPluginKind = new(
            "AUP0001",
            "プラグイン種別インターフェースが実装されていません",
            "[AviUtl2Plugin] クラス '{0}' はプラグイン種別インターフェース (IInputPlugin<THandle> / IOutputPlugin / IScriptModulePlugin / ICommonPlugin) を実装する必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultiplePluginKinds = new(
            "AUP0002",
            "複数のプラグイン種別インターフェースが実装されています",
            "[AviUtl2Plugin] クラス '{0}' は複数のプラグイン種別インターフェースを実装しています。1クラスにつき1種別のみ実装できます",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InputWithoutMedia = new(
            "AUP0003",
            "入力能力インターフェースが実装されていません",
            "入力プラグイン '{0}' は IInputVideo<THandle> か IInputAudio<THandle> の少なくとも一方を実装する必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OutputWithoutMedia = new(
            "AUP0004",
            "出力対応マーカーインターフェースが実装されていません",
            "出力プラグイン '{0}' は IOutputVideo か IOutputAudio の少なくとも一方を実装する必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ImageOnlyWithoutVideo = new(
            "AUP0005",
            "IOutputImageOnly には IOutputVideo が必要です",
            "出力プラグイン '{0}' は IOutputImageOnly を実装する場合、IOutputVideo も実装する必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultiplePluginClasses = new(
            "AUP0006",
            "複数の [AviUtl2Plugin] クラスが定義されています",
            "アセンブリ内に複数の [AviUtl2Plugin] クラスが定義されています ('{0}')。NativeAOTのエクスポート名は一意である必要があるため、1アセンブリにつき1クラスのみ定義できます",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ScriptModuleWithoutFunctions = new(
            "AUP0007",
            "[ScriptFunction] メソッドが定義されていません",
            "スクリプトモジュール '{0}' は [ScriptFunction] を付与したメソッドを1つ以上定義する必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidScriptFunction = new(
            "AUP0008",
            "[ScriptFunction] メソッドのシグネチャが不正です",
            "'{0}.{1}' のシグネチャが不正です。[ScriptFunction] メソッドは public/internal のインスタンスメソッドで 'void Method(ScriptModuleContext context)' である必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }

    internal sealed class ScriptFunctionInfo
    {
        public string MethodName { get; }
        public string ScriptName { get; }

        public ScriptFunctionInfo(string methodName, string scriptName)
        {
            MethodName = methodName;
            ScriptName = scriptName;
        }
    }

    internal sealed class PluginModel
    {
        public string ClassName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public Location Location { get; set; } = Location.None;

        public string FullClassName => Namespace.Length > 0
            ? $"global::{Namespace}.{ClassName}"
            : $"global::{ClassName}";

        // 種別
        public bool IsInput { get; set; }
        public bool IsOutput { get; set; }
        public bool IsScriptModule { get; set; }
        public bool IsCommon { get; set; }
        public string? HandleType { get; set; }

        // ScriptModuleの公開関数
        public System.Collections.Generic.List<ScriptFunctionInfo> ScriptFunctions { get; } = new();
        public System.Collections.Generic.List<string> InvalidScriptFunctions { get; } = new();

        // Input能力
        public bool HasInputVideo { get; set; }
        public bool HasInputAudio { get; set; }
        public bool HasInputConcurrent { get; set; }
        public bool HasInputMultiTrack { get; set; }
        public bool HasInputTimeToFrame { get; set; }
        public bool HasInputConfigDialog { get; set; }

        // Output能力
        public bool HasOutputVideo { get; set; }
        public bool HasOutputAudio { get; set; }
        public bool HasOutputImageOnly { get; set; }
        public bool HasOutputConfigDialog { get; set; }
        public bool HasOutputConfigText { get; set; }
        public bool HasOutputProjectConfig { get; set; }

        // ホストサービス機能
        public bool HasLogger { get; set; }
        public bool HasConfig { get; set; }
        public bool HasCache { get; set; }
        public bool HasLifecycle { get; set; }
        public bool HasRequiredVersion { get; set; }
    }
}
