using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Taix.PageState.Generator;

[Generator]
public sealed class PageStateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 注册语法接收器，收集带有 GeneratePageState 特性的类
        var viewModelClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, _) => GetClassDeclaration(ctx))
            .Where(static (classDecl) => classDecl is not null)
            .Collect();

        context.RegisterSourceOutput(viewModelClasses, static (spc, classes) =>
        {
            if (classes.IsDefaultOrEmpty) return;

            foreach (var classDecl in classes)
            {
                if (classDecl is null) continue;

                var source = GeneratePartialClass(classDecl, spc.CancellationToken);
                if (source is not null)
                {
                    var hintName = $"{classDecl.ClassName}.g.cs";
                    spc.AddSource(hintName, source);
                }
            }
        });
    }

    /// <summary>
    /// 判断语法节点是否为候选类
    /// </summary>
    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Count > 0;
    }

    /// <summary>
    /// 从语法上下文中提取类声明信息
    /// </summary>
    private static ClassDeclarationInfo? GetClassDeclaration(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);

        if (classSymbol is null) return null;

        // 检查是否有 GeneratePageState 特性
        var hasGenerateAttribute = classSymbol.GetAttributes().Any(attr =>
        {
            var attrName = attr.AttributeClass?.Name;
            return attrName == "GeneratePageStateAttribute" ||
                   attrName == "GeneratePageState";
        });

        if (!hasGenerateAttribute) return null;

        // 收集带有 PageState 特性的属性
        var stateProperties = new List<PropertyInfo>();
        var dataProperties = new List<PropertyInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property) continue;

            var pageStateAttr = property.GetAttributes().FirstOrDefault(attr =>
            {
                var attrName = attr.AttributeClass?.Name;
                return attrName == "PageStateAttribute" || attrName == "PageState";
            });

            if (pageStateAttr is null) continue;

            var propInfo = new PropertyInfo
            {
                Name = property.Name,
                Type = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                LookupFrom = GetNamedArgumentValue(pageStateAttr, "LookupFrom"),
                LookupBy = GetNamedArgumentValue(pageStateAttr, "LookupBy") ?? "Id",
                IsDataCache = GetNamedArgumentBoolValue(pageStateAttr, "DataCache")
            };

            if (propInfo.IsDataCache)
                dataProperties.Add(propInfo);
            else
                stateProperties.Add(propInfo);
        }

        // 获取命名空间
        var namespaceName = classSymbol.ContainingNamespace?.ToDisplayString() ?? "";

        return new ClassDeclarationInfo
        {
            ClassName = classSymbol.Name,
            Namespace = namespaceName,
            StateProperties = stateProperties,
            DataProperties = dataProperties,
            IsPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
        };
    }

    /// <summary>
    /// 获取特性命名参数的字符串值
    /// </summary>
    private static string? GetNamedArgumentValue(AttributeData attr, string paramName)
    {
        var arg = attr.NamedArguments.FirstOrDefault(x => x.Key == paramName);
        if (arg.Value.IsNull) return null;
        return arg.Value.Value?.ToString();
    }

    /// <summary>
    /// 获取特性命名参数的布尔值
    /// </summary>
    private static bool GetNamedArgumentBoolValue(AttributeData attr, string paramName)
    {
        var arg = attr.NamedArguments.FirstOrDefault(x => x.Key == paramName);
        if (arg.Value.IsNull) return false;
        return arg.Value.Value is bool b && b;
    }

    /// <summary>
    /// 生成 partial class 代码
    /// </summary>
    private static string? GeneratePartialClass(ClassDeclarationInfo classInfo, System.Threading.CancellationToken ct)
    {
        if (!classInfo.IsPartial)
        {
            // 类必须是 partial，否则无法生成扩展
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// ==================================================================");
        sb.AppendLine("// 自动生成代码 - 请勿手动修改");
        sb.AppendLine("// 由 Taix.PageState.Generator 生成");
        sb.AppendLine("// ==================================================================");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Linq;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(classInfo.Namespace))
        {
            sb.AppendLine($"namespace {classInfo.Namespace};");
            sb.AppendLine();
        }

        // 生成状态类
        if (classInfo.StateProperties.Count > 0)
        {
            GenerateStateRecord(sb, classInfo);
        }

        // 生成数据类
        if (classInfo.DataProperties.Count > 0)
        {
            GenerateDataRecord(sb, classInfo);
        }

        // 生成 partial class
        sb.AppendLine($"public partial class {classInfo.ClassName}");
        sb.AppendLine("{");

        // 常量
        sb.AppendLine($"    private const string StateKey = nameof({classInfo.ClassName});");

        if (classInfo.DataProperties.Count > 0)
        {
            sb.AppendLine($"    private const string DataKey = nameof({classInfo.ClassName}) + \"_Data\";");
        }

        sb.AppendLine();

        // 生成 CaptureState 方法
        if (classInfo.StateProperties.Count > 0)
        {
            GenerateCaptureStateMethod(sb, classInfo);
        }

        // 生成 ApplyState 方法
        if (classInfo.StateProperties.Count > 0)
        {
            GenerateApplyStateMethod(sb, classInfo);
        }

        // 生成 CaptureData 方法
        if (classInfo.DataProperties.Count > 0)
        {
            GenerateCaptureDataMethod(sb, classInfo);
        }

        // 生成 ApplyData 方法
        if (classInfo.DataProperties.Count > 0)
        {
            GenerateApplyDataMethod(sb, classInfo);
        }

        // 生成 TryRestoreState 方法
        GenerateTryRestoreStateMethod(sb, classInfo);

        // 生成 SaveState 方法
        GenerateSaveStateMethod(sb, classInfo);

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// 生成状态记录类
    /// </summary>
    private static void GenerateStateRecord(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// {classInfo.ClassName} 的筛选状态");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public record {classInfo.ClassName}State");
        sb.AppendLine("{");

        foreach (var prop in classInfo.StateProperties)
        {
            var statePropName = GetStatePropertyName(prop);
            var statePropType = GetStatePropertyType(prop);

            sb.AppendLine($"    public {statePropType} {statePropName} {{ get; init; }}");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成数据记录类
    /// </summary>
    private static void GenerateDataRecord(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// {classInfo.ClassName} 的数据缓存状态");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public record {classInfo.ClassName}Data");
        sb.AppendLine("{");

        foreach (var prop in classInfo.DataProperties)
        {
            sb.AppendLine($"    public {prop.Type} {prop.Name} {{ get; init; }} = default!;");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成 CaptureState 方法
    /// </summary>
    private static void GenerateCaptureStateMethod(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// 捕获当前筛选状态");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {classInfo.ClassName}State CaptureState()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {classInfo.ClassName}State");
        sb.AppendLine("        {");

        for (int i = 0; i < classInfo.StateProperties.Count; i++)
        {
            var prop = classInfo.StateProperties[i];
            var statePropName = GetStatePropertyName(prop);
            var accessExpr = GetCaptureExpression(prop);

            sb.AppendLine($"            {statePropName} = {accessExpr}{(i < classInfo.StateProperties.Count - 1 ? "," : "")}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成 ApplyState 方法
    /// </summary>
    private static void GenerateApplyStateMethod(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// 应用筛选状态");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public void ApplyState({classInfo.ClassName}State state)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (state == null) return;");
        sb.AppendLine();

        foreach (var prop in classInfo.StateProperties)
        {
            var assignExpr = GetApplyExpression(prop);
            sb.AppendLine($"        {assignExpr}");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成 CaptureData 方法
    /// </summary>
    private static void GenerateCaptureDataMethod(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// 捕获当前数据缓存");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {classInfo.ClassName}Data CaptureData()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {classInfo.ClassName}Data");
        sb.AppendLine("        {");

        for (int i = 0; i < classInfo.DataProperties.Count; i++)
        {
            var prop = classInfo.DataProperties[i];
            sb.AppendLine($"            {prop.Name} = {prop.Name}{(i < classInfo.DataProperties.Count - 1 ? "," : "")}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成 ApplyData 方法
    /// </summary>
    private static void GenerateApplyDataMethod(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// 应用数据缓存");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public void ApplyData({classInfo.ClassName}Data data)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (data == null) return;");
        sb.AppendLine();

        foreach (var prop in classInfo.DataProperties)
        {
            sb.AppendLine($"        {prop.Name} = data.{prop.Name};");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成 TryRestoreState 方法
    /// </summary>
    private static void GenerateTryRestoreStateMethod(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// 尝试从缓存中恢复状态（仅在返回导航时）");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"navigation\">导航服务</param>");
        sb.AppendLine($"    /// <param name=\"stateService\">状态服务</param>");
        sb.AppendLine($"    /// <returns>是否成功恢复状态</returns>");
        sb.AppendLine($"    public bool TryRestoreState(Taix.Client.Servicers.Interfaces.INavigationService navigation, Taix.Client.Servicers.Interfaces.IStateService stateService)");
        sb.AppendLine("    {");
        sb.AppendLine("        System.Diagnostics.Debug.WriteLine($\"[{nameof(" + classInfo.ClassName + ")}] TryRestoreState - IsNavigatingBack: {navigation.IsNavigatingBack}\");");
        sb.AppendLine("        if (!navigation.IsNavigatingBack) return false;");
        sb.AppendLine();

        if (classInfo.StateProperties.Count > 0)
        {
            sb.AppendLine($"        var state = stateService.Get<string, {classInfo.ClassName}State>(StateKey);");
            sb.AppendLine("        System.Diagnostics.Debug.WriteLine($\"[{nameof(" + classInfo.ClassName + ")}] State from cache: {(state == null ? \"null\" : \"found\")}\");");
            sb.AppendLine("        if (state == null) return false;");
            sb.AppendLine();
            sb.AppendLine("        IsRestoringState = true;");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            ApplyState(state);");
            sb.AppendLine("        }");
            sb.AppendLine("        finally");
            sb.AppendLine("        {");
            sb.AppendLine("            IsRestoringState = false;");
            sb.AppendLine("        }");
            sb.AppendLine("        System.Diagnostics.Debug.WriteLine($\"[{nameof(" + classInfo.ClassName + ")}] ApplyState completed\");");
        }

        if (classInfo.DataProperties.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"        var data = stateService.GetData<string, {classInfo.ClassName}Data>(DataKey);");
            sb.AppendLine("        System.Diagnostics.Debug.WriteLine($\"[{nameof(" + classInfo.ClassName + ")}] Data from cache: {(data == null ? \"null\" : \"found\")}\");");
            sb.AppendLine("        if (data != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            IsRestoringState = true;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                ApplyData(data);");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                IsRestoringState = false;");
            sb.AppendLine("            }");
            sb.AppendLine("            System.Diagnostics.Debug.WriteLine($\"[{nameof(" + classInfo.ClassName + ")}] ApplyData completed\");");
            sb.AppendLine("        }");
        }

        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成 SaveState 方法
    /// </summary>
    private static void GenerateSaveStateMethod(StringBuilder sb, ClassDeclarationInfo classInfo)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// 保存当前状态到缓存");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"stateService\">状态服务</param>");
        sb.AppendLine($"    public void SaveState(Taix.Client.Servicers.Interfaces.IStateService stateService)");
        sb.AppendLine("    {");
        sb.AppendLine($"        System.Diagnostics.Debug.WriteLine($\"[" + classInfo.ClassName + "] SaveState called\");");

        if (classInfo.StateProperties.Count > 0)
        {
            sb.AppendLine("        var capturedState = CaptureState();");
            sb.AppendLine($"        System.Diagnostics.Debug.WriteLine($\"[" + classInfo.ClassName + "] Saving state to key: \" + StateKey);");
            sb.AppendLine("        stateService.Set(StateKey, capturedState);");
        }

        if (classInfo.DataProperties.Count > 0)
        {
            sb.AppendLine("        stateService.SetData(DataKey, CaptureData());");
            sb.AppendLine($"        System.Diagnostics.Debug.WriteLine($\"[" + classInfo.ClassName + "] Data saved to key: \" + DataKey);");
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// 获取状态属性名称（用于状态类）
    /// </summary>
    private static string GetStatePropertyName(PropertyInfo prop)
    {
        // 对于需要查找的属性，存储其 Id
        if (!string.IsNullOrEmpty(prop.LookupFrom))
        {
            return $"{prop.Name}_Id";
        }
        return prop.Name;
    }

    /// <summary>
    /// 获取状态属性类型
    /// </summary>
    private static string GetStatePropertyType(PropertyInfo prop)
    {
        // 对于需要查找的属性，存储 int 类型的 Id
        if (!string.IsNullOrEmpty(prop.LookupFrom))
        {
            return "int";
        }
        return prop.Type;
    }

    /// <summary>
    /// 获取捕获表达式
    /// </summary>
    private static string GetCaptureExpression(PropertyInfo prop)
    {
        if (!string.IsNullOrEmpty(prop.LookupFrom))
        {
            // 存储 Id
            return $"{prop.Name}?.{prop.LookupBy} ?? 0";
        }
        return prop.Name;
    }

    /// <summary>
    /// 获取应用表达式（赋值语句）
    /// </summary>
    private static string GetApplyExpression(PropertyInfo prop)
    {
        if (!string.IsNullOrEmpty(prop.LookupFrom))
        {
            // 从集合中查找
            var statePropName = GetStatePropertyName(prop);
            return $"if ({prop.LookupFrom} != null) {prop.Name} = {prop.LookupFrom}.FirstOrDefault(x => x.{prop.LookupBy} == state.{statePropName}) ?? {prop.LookupFrom}.FirstOrDefault()!;";
        }
        return $"{prop.Name} = state.{prop.Name};";
    }

    #region 辅助类型

    private sealed class ClassDeclarationInfo
    {
        public string ClassName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public List<PropertyInfo> StateProperties { get; set; } = new();
        public List<PropertyInfo> DataProperties { get; set; } = new();
        public bool IsPartial { get; set; }
    }

    private sealed class PropertyInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IsNullable { get; set; }
        public string? LookupFrom { get; set; }
        public string LookupBy { get; set; } = "Id";
        public bool IsDataCache { get; set; }
    }

    #endregion
}
