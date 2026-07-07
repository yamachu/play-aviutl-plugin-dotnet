using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
                    case "IFilterPlugin":
                        model.IsFilter = true;
                        break;

                    // Filter能力
                    case "IFilterVideo":
                        model.HasFilterVideo = true;
                        break;
                    case "IFilterAudio":
                        model.HasFilterAudio = true;
                        break;
                    case "IFilterInput":
                        model.HasFilterInput = true;
                        break;
                    case "IFilterObject":
                        model.HasFilterObject = true;
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

            model.IsPartialClass = context.TargetNode is ClassDeclarationSyntax classDeclaration &&
                classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

            CollectScriptFunctions(classSymbol, model);
            CollectFilterItems(classSymbol, model);

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

        private static readonly string[] FilterPropertyAttributeNames =
        {
            "FilterTrackAttribute", "FilterCheckAttribute", "FilterColorAttribute", "FilterSelectAttribute",
            "FilterFileAttribute", "FilterFolderAttribute", "FilterStringAttribute", "FilterTextAttribute",
        };

        /// <summary>
        /// [FilterTrack] 等が付与されたプロパティ / [FilterButton] メソッドを宣言順に収集する
        /// [FilterGroup] / [FilterSeparator] は併記されたメンバーの直前に項目として挿入する
        /// </summary>
        private static void CollectFilterItems(INamedTypeSymbol classSymbol, PluginModel model)
        {
            foreach (var member in classSymbol.GetMembers())
            {
                var attributes = member.GetAttributes()
                    .Where(a => a.AttributeClass?.ContainingNamespace?.ToDisplayString() == AbstractionsNamespace)
                    .ToList();
                if (attributes.Count == 0)
                {
                    continue;
                }

                var itemAttribute = attributes.FirstOrDefault(a =>
                    FilterPropertyAttributeNames.Contains(a.AttributeClass!.Name) ||
                    a.AttributeClass!.Name == "FilterButtonAttribute");
                if (itemAttribute == null)
                {
                    continue;
                }

                // グループ・セパレーターを項目の直前に挿入
                var group = attributes.FirstOrDefault(a => a.AttributeClass!.Name == "FilterGroupAttribute");
                if (group != null)
                {
                    model.FilterItems.Add(new FilterItemInfo
                    {
                        Kind = "Group",
                        Name = group.ConstructorArguments.FirstOrDefault().Value as string ?? "",
                        GroupDefaultVisible = GetNamedArgument(group, "DefaultVisible") is bool visible ? visible : true,
                    });
                }
                var separator = attributes.FirstOrDefault(a => a.AttributeClass!.Name == "FilterSeparatorAttribute");
                if (separator != null)
                {
                    model.FilterItems.Add(new FilterItemInfo
                    {
                        Kind = "Separator",
                        Name = separator.ConstructorArguments.FirstOrDefault().Value as string ?? "",
                    });
                }

                if (itemAttribute.AttributeClass!.Name == "FilterButtonAttribute")
                {
                    if (member is not IMethodSymbol method ||
                        !method.ReturnsVoid || method.IsStatic || method.Parameters.Length != 0 ||
                        method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                    {
                        model.InvalidFilterItems.Add(member.Name);
                        continue;
                    }
                    model.FilterItems.Add(new FilterItemInfo
                    {
                        Kind = "Button",
                        MemberName = method.Name,
                        Name = itemAttribute.ConstructorArguments.FirstOrDefault().Value as string ?? method.Name,
                    });
                    continue;
                }

                // 値を持つ項目: get専用のpartialプロパティが必要
                if (member is not IPropertySymbol property ||
                    !property.IsPartialDefinition || property.IsStatic ||
                    property.GetMethod == null || property.SetMethod != null ||
                    property.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                {
                    model.InvalidFilterItems.Add(member.Name);
                    continue;
                }

                var kind = itemAttribute.AttributeClass.Name.Substring("Filter".Length);
                kind = kind.Substring(0, kind.Length - "Attribute".Length); // Track/Check/Color/Select/File/Folder/String/Text

                var expectedType = kind switch
                {
                    "Track" => SpecialType.System_Double,
                    "Check" => SpecialType.System_Boolean,
                    "Color" or "Select" => SpecialType.System_Int32,
                    _ => SpecialType.System_String,
                };
                if (property.Type.SpecialType != expectedType)
                {
                    model.InvalidFilterItems.Add(member.Name);
                    continue;
                }

                var item = new FilterItemInfo
                {
                    Kind = kind,
                    MemberName = property.Name,
                    Name = itemAttribute.ConstructorArguments.FirstOrDefault().Value as string ?? property.Name,
                    Accessibility = property.DeclaredAccessibility == Accessibility.Internal ? "internal" : "public",
                };

                switch (kind)
                {
                    case "Track":
                        item.Default = GetNamedArgument(itemAttribute, "Default") is double d ? d : 0.0;
                        item.Min = GetNamedArgument(itemAttribute, "Min") is double min ? min : 0.0;
                        item.Max = GetNamedArgument(itemAttribute, "Max") is double max ? max : 100.0;
                        item.Step = GetNamedArgument(itemAttribute, "Step") is double step ? step : 1.0;
                        item.ZeroDisplay = GetNamedArgument(itemAttribute, "ZeroDisplay") as string;
                        item.SliderRatio = GetNamedArgument(itemAttribute, "SliderRatio") is double ratio ? ratio : 1.0;
                        break;
                    case "Check":
                        item.DefaultBool = GetNamedArgument(itemAttribute, "Default") is bool b && b;
                        break;
                    case "Color":
                    case "Select":
                        item.DefaultInt = GetNamedArgument(itemAttribute, "Default") is int i ? i : 0;
                        break;
                    default:
                        item.DefaultString = GetNamedArgument(itemAttribute, "Default") as string ?? "";
                        if (kind == "File")
                        {
                            item.FileFilter = GetNamedArgument(itemAttribute, "FileFilter") as string ?? "";
                        }
                        break;
                }

                if (kind == "Select")
                {
                    foreach (var selectItem in attributes.Where(a => a.AttributeClass!.Name == "FilterSelectItemAttribute"))
                    {
                        var name = selectItem.ConstructorArguments.ElementAtOrDefault(0).Value as string ?? "";
                        var value = selectItem.ConstructorArguments.ElementAtOrDefault(1).Value is int v ? v : 0;
                        item.SelectItems.Add((name, value));
                    }
                    if (item.SelectItems.Count == 0)
                    {
                        model.InvalidFilterItems.Add(member.Name);
                        continue;
                    }
                }

                model.FilterItems.Add(item);
            }
        }

        private static object? GetNamedArgument(AttributeData attribute, string name)
            => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value;

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
                    : model.IsFilter ? GenerateFilterNativeLibrary(model)
                    : GenerateCommonNativeLibrary(model);
                context.AddSource($"{model.ClassName}NativeLibrary.g.cs", SourceText.From(source, Encoding.UTF8));

                // フィルタ設定項目のpartialプロパティ実装を生成
                if (model.IsFilter && model.FilterItems.Any(f => f.IsValueProperty))
                {
                    context.AddSource($"{model.ClassName}.FilterItems.g.cs", SourceText.From(GenerateFilterItemProperties(model), Encoding.UTF8));
                }
            }
        }

        private static bool Validate(SourceProductionContext context, PluginModel model)
        {
            var kindCount = (model.IsInput ? 1 : 0) + (model.IsOutput ? 1 : 0) + (model.IsScriptModule ? 1 : 0) + (model.IsCommon ? 1 : 0) + (model.IsFilter ? 1 : 0);

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

            if (model.InvalidFilterItems.Count > 0)
            {
                foreach (var member in model.InvalidFilterItems)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidFilterItem, model.Location, model.ClassName, member));
                }
                return false;
            }

            if (model.IsFilter && !model.HasFilterVideo && !model.HasFilterAudio)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FilterWithoutMedia, model.Location, model.ClassName));
                return false;
            }

            if (model.IsFilter && model.FilterItems.Any(f => f.IsValueProperty) && !model.IsPartialClass)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FilterClassNotPartial, model.Location, model.ClassName));
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

        private static string GenerateFilterNativeLibrary(PluginModel model)
        {
            var cls = model.FullClassName;
            var items = model.FilterItems;

            var flags = new List<string>();
            if (model.HasFilterVideo) flags.Add("FilterPluginTableFlag.Video");
            if (model.HasFilterAudio) flags.Add("FilterPluginTableFlag.Audio");
            if (model.HasFilterInput) flags.Add("FilterPluginTableFlag.Input");
            if (model.HasFilterObject) flags.Add("FilterPluginTableFlag.Filter");
            var flagExpression = flags.Count > 0 ? string.Join(" | ", flags) : "FilterPluginTableFlag.None";

            var builder = new StringBuilder();
            builder.Append($@"// <auto-generated/>
#nullable enable
using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.Filter2;

{(model.Namespace.Length > 0 ? $"namespace {model.Namespace};" : "")}

public static unsafe class {model.ClassName}NativeLibrary
{{
    private static readonly {cls} plugin = new {cls}();
    private static IntPtr pluginTablePtr;
");

            // 値を持つ項目のネイティブ構造体ポインタ(partialプロパティ実装から参照される)
            foreach (var item in items.Where(i => i.IsValueProperty))
            {
                builder.Append($@"
    private static {NativeItemStructName(item.Kind)}* item_{item.MemberName};");
            }

            builder.Append($@"

    [UnmanagedCallersOnly(EntryPoint = ""GetFilterPluginTable"", CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    public static IntPtr GetFilterPluginTable()
    {{
        if (pluginTablePtr == IntPtr.Zero)
        {{
            // FILTER_ITEM_XXXポインタを列挙してnull終端したリスト
            var items = (void**)Marshal.AllocHGlobal(sizeof(void*) * {items.Count + 1});
");

            for (var i = 0; i < items.Count; i++)
            {
                builder.Append(GenerateFilterItemInitializer(items[i], i));
            }

            builder.Append($@"            items[{items.Count}] = null;

            var table = (FILTER_PLUGIN_TABLE*)Marshal.AllocHGlobal(sizeof(FILTER_PLUGIN_TABLE));
            table->flag = {flagExpression};
            table->name = Marshal.StringToHGlobalUni({cls}.Name);
            var label = GetLabel<{cls}>();
            table->label = label != null ? Marshal.StringToHGlobalUni(label) : IntPtr.Zero;
            table->information = Marshal.StringToHGlobalUni({cls}.Information);
            table->items = items;
            table->func_proc_video = {(model.HasFilterVideo ? "&FuncProcVideo" : "null")};
            table->func_proc_audio = {(model.HasFilterAudio ? "&FuncProcAudio" : "null")};
            pluginTablePtr = (IntPtr)table;
        }}
        return pluginTablePtr;
    }}

    // Labelはstatic virtual(既定実装あり)のためジェネリック経由でアクセスする
    private static string? GetLabel<TPlugin>() where TPlugin : global::{AbstractionsNamespace}.IFilterPlugin
        => TPlugin.Label;
");

            if (model.HasFilterVideo)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncProcVideo(IntPtr video)
    {{
        if (video == IntPtr.Zero) return false;
        try
        {{
            return ((global::{AbstractionsNamespace}.IFilterVideo)plugin).ProcVideo(new global::{AbstractionsNamespace}.FilterVideoContext(video));
        }}
        catch
        {{
            return false;
        }}
    }}
");
            }

            if (model.HasFilterAudio)
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static bool FuncProcAudio(IntPtr audio)
    {{
        if (audio == IntPtr.Zero) return false;
        try
        {{
            return ((global::{AbstractionsNamespace}.IFilterAudio)plugin).ProcAudio(new global::{AbstractionsNamespace}.FilterAudioContext(audio));
        }}
        catch
        {{
            return false;
        }}
    }}
");
            }

            // ボタンのトランポリン (コールバック引数はEDIT_SECTION*)
            foreach (var item in items.Where(i => i.Kind == "Button"))
            {
                builder.Append($@"
    [UnmanagedCallersOnly(CallConvs = new[] {{ typeof(System.Runtime.CompilerServices.CallConvStdcall) }})]
    private static void FilterButton_{item.MemberName}(IntPtr edit)
    {{
        try
        {{
            plugin.{item.MemberName}();
        }}
        catch
        {{
        }}
    }}
");
            }

            // partialプロパティ実装から呼ばれる値アクセサ
            foreach (var item in items.Where(i => i.IsValueProperty))
            {
                var accessor = item.Kind switch
                {
                    "Track" => $"internal static double Get_{item.MemberName}() => item_{item.MemberName} != null ? item_{item.MemberName}->value : {FormatDouble(item.Default)};",
                    "Check" => $"internal static bool Get_{item.MemberName}() => item_{item.MemberName} != null ? item_{item.MemberName}->value != 0 : {(item.DefaultBool ? "true" : "false")};",
                    "Color" or "Select" => $"internal static int Get_{item.MemberName}() => item_{item.MemberName} != null ? item_{item.MemberName}->value : {item.DefaultInt};",
                    _ => $"internal static string Get_{item.MemberName}() => item_{item.MemberName} != null ? (Marshal.PtrToStringUni(item_{item.MemberName}->value) ?? \"\") : \"{EscapeLiteral(item.DefaultString)}\";",
                };
                builder.Append($@"
    {accessor}
");
            }

            AppendFeatureExports(builder, model);

            builder.Append(@"}
");
            return builder.ToString();
        }

        private static string NativeItemStructName(string kind) => kind switch
        {
            "Track" => "FILTER_ITEM_TRACK",
            "Check" => "FILTER_ITEM_CHECK",
            "Color" => "FILTER_ITEM_COLOR",
            "Select" => "FILTER_ITEM_SELECT",
            "File" => "FILTER_ITEM_FILE",
            _ => "FILTER_ITEM_STRING_LIKE", // Folder/String/Text
        };

        private static string GenerateFilterItemInitializer(FilterItemInfo item, int index)
        {
            var name = EscapeLiteral(item.Name);
            switch (item.Kind)
            {
                case "Group":
                    return $@"            {{
                var it = (FILTER_ITEM_GROUP*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_GROUP));
                it->type = Marshal.StringToHGlobalUni(""group"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->default_visible = {(item.GroupDefaultVisible ? "1" : "0")};
                items[{index}] = it;
            }}
";
                case "Separator":
                    return $@"            {{
                var it = (FILTER_ITEM_SEPARATOR*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_SEPARATOR));
                it->type = Marshal.StringToHGlobalUni(""separator"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                items[{index}] = it;
            }}
";
                case "Button":
                    return $@"            {{
                var it = (FILTER_ITEM_BUTTON*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_BUTTON));
                it->type = Marshal.StringToHGlobalUni(""button"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->callback = &FilterButton_{item.MemberName};
                items[{index}] = it;
            }}
";
                case "Track":
                    var zeroDisplay = item.ZeroDisplay == null
                        ? "IntPtr.Zero"
                        : $"Marshal.StringToHGlobalUni(\"{EscapeLiteral(item.ZeroDisplay)}\")";
                    return $@"            {{
                var it = (FILTER_ITEM_TRACK*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_TRACK));
                it->type = Marshal.StringToHGlobalUni(""track2"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->value = {FormatDouble(item.Default)};
                it->s = {FormatDouble(item.Min)};
                it->e = {FormatDouble(item.Max)};
                it->step = {FormatDouble(item.Step)};
                it->zero_display = {zeroDisplay};
                it->slider_ratio = {FormatDouble(item.SliderRatio)};
                item_{item.MemberName} = it;
                items[{index}] = it;
            }}
";
                case "Check":
                    return $@"            {{
                var it = (FILTER_ITEM_CHECK*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_CHECK));
                it->type = Marshal.StringToHGlobalUni(""check"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->value = {(item.DefaultBool ? "1" : "0")};
                item_{item.MemberName} = it;
                items[{index}] = it;
            }}
";
                case "Color":
                    return $@"            {{
                var it = (FILTER_ITEM_COLOR*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_COLOR));
                it->type = Marshal.StringToHGlobalUni(""color"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->value = {item.DefaultInt};
                item_{item.MemberName} = it;
                items[{index}] = it;
            }}
";
                case "Select":
                    var listBuilder = new StringBuilder();
                    listBuilder.Append($@"                var list = (FILTER_ITEM_SELECT_ITEM*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_SELECT_ITEM) * {item.SelectItems.Count + 1});
");
                    for (var i = 0; i < item.SelectItems.Count; i++)
                    {
                        listBuilder.Append($@"                list[{i}].name = Marshal.StringToHGlobalUni(""{EscapeLiteral(item.SelectItems[i].Name)}"");
                list[{i}].value = {item.SelectItems[i].Value};
");
                    }
                    listBuilder.Append($@"                list[{item.SelectItems.Count}].name = IntPtr.Zero;
                list[{item.SelectItems.Count}].value = 0;
");
                    return $@"            {{
{listBuilder}                var it = (FILTER_ITEM_SELECT*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_SELECT));
                it->type = Marshal.StringToHGlobalUni(""select"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->value = {item.DefaultInt};
                it->list = list;
                item_{item.MemberName} = it;
                items[{index}] = it;
            }}
";
                case "File":
                    return $@"            {{
                var it = (FILTER_ITEM_FILE*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_FILE));
                it->type = Marshal.StringToHGlobalUni(""file"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->value = Marshal.StringToHGlobalUni(""{EscapeLiteral(item.DefaultString)}"");
                it->filefilter = Marshal.StringToHGlobalUni(""{EscapeLiteral(item.FileFilter)}"");
                item_{item.MemberName} = it;
                items[{index}] = it;
            }}
";
                default: // Folder/String/Text
                    var type = item.Kind switch
                    {
                        "Folder" => "folder",
                        "Text" => "text",
                        _ => "string",
                    };
                    return $@"            {{
                var it = (FILTER_ITEM_STRING_LIKE*)Marshal.AllocHGlobal(sizeof(FILTER_ITEM_STRING_LIKE));
                it->type = Marshal.StringToHGlobalUni(""{type}"");
                it->name = Marshal.StringToHGlobalUni(""{name}"");
                it->value = Marshal.StringToHGlobalUni(""{EscapeLiteral(item.DefaultString)}"");
                item_{item.MemberName} = it;
                items[{index}] = it;
            }}
";
            }
        }

        /// <summary>
        /// 値を持つ設定項目のpartialプロパティ実装を生成する
        /// getterはネイティブitem構造体の現在値を直接読むため、常にホストの最新値が取得できる
        /// </summary>
        private static string GenerateFilterItemProperties(PluginModel model)
        {
            var builder = new StringBuilder();
            builder.Append($@"// <auto-generated/>
#nullable enable

{(model.Namespace.Length > 0 ? $"namespace {model.Namespace};" : "")}

partial class {model.ClassName}
{{
");
            foreach (var item in model.FilterItems.Where(i => i.IsValueProperty))
            {
                var type = item.Kind switch
                {
                    "Track" => "double",
                    "Check" => "bool",
                    "Color" or "Select" => "int",
                    _ => "string",
                };
                builder.Append($@"    {item.Accessibility} partial {type} {item.MemberName} => {model.ClassName}NativeLibrary.Get_{item.MemberName}();
");
            }
            builder.Append(@"}
");
            return builder.ToString();
        }

        private static string FormatDouble(double value)
            => value.ToString("R", CultureInfo.InvariantCulture) + "d";

        private static string EscapeLiteral(string value)
            => value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\0", "\\0").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

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

        public static readonly DiagnosticDescriptor InvalidFilterItem = new(
            "AUP0009",
            "フィルタ設定項目の宣言が不正です",
            "'{0}.{1}' の宣言が不正です。値を持つ項目は属性に対応した型(Track=double/Check=bool/Color,Select=int/その他=string)の get専用 partial プロパティ、[FilterButton] は引数なしの void インスタンスメソッドである必要があります (Selectは [FilterSelectItem] が1つ以上必要)",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor FilterWithoutMedia = new(
            "AUP0010",
            "フィルタ処理能力インターフェースが実装されていません",
            "フィルタプラグイン '{0}' は IFilterVideo か IFilterAudio の少なくとも一方を実装する必要があります",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor FilterClassNotPartial = new(
            "AUP0011",
            "フィルタ設定項目を持つクラスは partial である必要があります",
            "'{0}' は設定項目の partial プロパティ実装を生成するため partial クラスである必要があります",
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

    internal sealed class FilterItemInfo
    {
        // Group / Separator / Button / Track / Check / Color / Select / File / Folder / String / Text
        public string Kind { get; set; } = "";
        public string MemberName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Accessibility { get; set; } = "public";

        public double Default { get; set; }
        public double Min { get; set; }
        public double Max { get; set; } = 100.0;
        public double Step { get; set; } = 1.0;
        public double SliderRatio { get; set; } = 1.0;
        public string? ZeroDisplay { get; set; }
        public bool DefaultBool { get; set; }
        public int DefaultInt { get; set; }
        public string DefaultString { get; set; } = "";
        public string FileFilter { get; set; } = "";
        public System.Collections.Generic.List<(string Name, int Value)> SelectItems { get; } = new();
        public bool GroupDefaultVisible { get; set; } = true;

        /// <summary>値を持つ項目(partialプロパティ実装を生成する項目)か</summary>
        public bool IsValueProperty => Kind is "Track" or "Check" or "Color" or "Select" or "File" or "Folder" or "String" or "Text";
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
        public bool IsFilter { get; set; }
        public string? HandleType { get; set; }
        public bool IsPartialClass { get; set; }

        // Filter能力・設定項目
        public bool HasFilterVideo { get; set; }
        public bool HasFilterAudio { get; set; }
        public bool HasFilterInput { get; set; }
        public bool HasFilterObject { get; set; }
        public System.Collections.Generic.List<FilterItemInfo> FilterItems { get; } = new();
        public System.Collections.Generic.List<string> InvalidFilterItems { get; } = new();

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
