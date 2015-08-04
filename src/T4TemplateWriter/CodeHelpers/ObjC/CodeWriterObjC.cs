﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vipr.Core.CodeModel;
using Vipr.T4TemplateWriter.Settings;
using Vipr.T4TemplateWriter.Extensions;

namespace Vipr.T4TemplateWriter.CodeHelpers.ObjC {
    public class CodeWriterObjC : CodeWriterBase {

        public CodeWriterObjC() : base() { }

        public CodeWriterObjC(OdcmModel model) : base(model)
        {
            TypeHelperObjC.Prefix = GetPrefix();
        }

        public string GetPrefix() {
            if (this.CurrentModel != null) {
                return ConfigurationService.Settings.NamespacePrefix + this.CurrentModel.EntityContainer.Name;
            } else {
                return ConfigurationService.Settings.NamespacePrefix;
            }
        }

        public override String WriteOpeningCommentLine() {
            return "/*******************************************************************************\n";
        }

        public override String WriteClosingCommentLine() {
            return "\n******************************************************************************/";
        }

        public override string WriteInlineCommentChar() {
            return "// ";
        }
        public string GetInterfaceLine(OdcmClass e) {

            string baseEntity = e.Base == null ? "MSOrcBaseEntity"
                              : GetPrefix() + e.Base.Name.Substring(e.Base.Name.LastIndexOf(".") + 1);

            var s = new StringBuilder();
            s.AppendFormat("#import \"{0}.h\"", baseEntity);

            s.AppendLine().AppendLine().AppendLine(GetHeaderDoc(e.Name))
            .AppendFormat("@interface {0}{1} : {2}", GetPrefix(), e.Name, baseEntity);

            return s.ToString();
        }

        public string GetHeaderDoc(string name) {

            var stringBuilder = new StringBuilder();
            stringBuilder.Append(@"/**");
            stringBuilder.AppendLine().AppendFormat(@"* The header for type {0}.", name);
            stringBuilder.AppendLine().AppendLine(@"*/");

            return stringBuilder.ToString();
        }

        public string GetImplementationDoc(string name) {

            var stringBuilder = new StringBuilder();
            stringBuilder.Append(@"/**");
            stringBuilder.AppendLine().AppendFormat(@"* The implementation file for type {0}.", name);
            stringBuilder.AppendLine().AppendLine(@"*/");

            return stringBuilder.ToString();
        }

        public string GetMethodDoc(string name, List<OdcmProperty> parameters) {
            return "";
        }

        public string GetParams(IEnumerable<OdcmProperty> parameters) {
            string param = "With";

            foreach (var p in parameters) {
                if (param == "With") {
                    param += string.Format("{0}:({2} {3}) {1}", char.ToUpper(p.Name[0]) + p.Name.Substring(1)
                                        , p.Name.ToLowerFirstChar(), p.Type.GetFullType(), (p.Type.IsComplex() ? "*" : ""));
                } else {
                    param += string.Format("{0}:({1} {2}) {1}", p.Name.ToLowerFirstChar(), p.Type.GetFullType(), (p.Type.IsComplex() ? "*" : ""));
                }
            }
            return param;
        }

        public string GetParamsForRaw(IEnumerable<string> parameters) {
            string param = "With";

            foreach (var p in parameters) {
                param += param == "With" ? string.Format("{0}:(NSString *) {1} ", char.ToUpper(p[0]) + p.Substring(1), p.ToLowerFirstChar()) :
                 string.Format("{0}:(NSString *) {0} ", p.ToLowerFirstChar());
            }

            param += parameters.Count() > 0 ? "callback" : "Callback";

            return param;
        }

        public string GetParam(OdcmProperty type) {
            if (type.IsComplex()) {
                return type.IsSystem() ? string.Empty : type.GetTypeString() + " *" + type.Name.ToLowerFirstChar();
            }

            return type.GetTypeString() + " " + type.Name;
        }

        public string GetParamRaw(string type) {
            return "NSString *" + type.ToLowerFirstChar();

        }

        public string GetType(OdcmType type) {
            if (type.IsComplex()) {
                return type.IsSystem() ? type.GetTypeString() : type.GetTypeString() + " *";
            }

            return type.GetTypeString();
        }

        public string GetImportsClass(List<OdcmProperty> references) {
            var imports = new StringBuilder();
            var classes = new StringBuilder();
            foreach (var r in references) {
                if (r.Type is OdcmEnum) {
                    imports.AppendFormat("#import \"{0}.h\"", r.Type.GetTypeString()).AppendLine();
                } else if (r.Type.IsComplex() && !r.Type.IsSystem()) {
                    classes.AppendFormat("@class {0};", r.Type.GetTypeString()).AppendLine();
                }
            }

            var classString = classes.AppendLine().ToString();
            var importsString = imports.ToString();
            return (string.IsNullOrWhiteSpace(importsString.Trim()) ? null : importsString) +
                (string.IsNullOrWhiteSpace(classString.Trim()) ? null : classString);
        }

        public string GetClass(OdcmProperty type) {
            return type.IsComplex() ? string.Format("[{0}{1} class]", GetPrefix(), type.GetTypeString()) : "nil";
        }

        public string GetParametersToJsonRaw(IEnumerable<string> parameters) {
            if (!parameters.Any()) { return new StringBuilder().AppendLine().ToString(); }

            var result = new StringBuilder();

            if (parameters.Any()) {
                result.Append("NSArray *parameters = [[NSArray alloc] initWithObjects:");

                foreach (var name in parameters) {
                    result.AppendLine().Append("                          ")
                    .AppendFormat("[[NSDictionary alloc] initWithObjectsAndKeys :{0},@\"{1}\", nil],", name.ToLowerFirstChar(), name);
                }

                result.Append(" nil];");
                result.AppendLine().AppendLine().Append("\t").Append("NSData* payload = " +
              "[[MSOrcBaseContainer generatePayloadWithParameters:parameters dependencyResolver:self.resolver] dataUsingEncoding:NSUTF8StringEncoding];");

                result.AppendLine().AppendLine().Append("\t").AppendLine("[request setContent:payload];").AppendLine();
            }

            return result.ToString();
        }

        public string GetParametersToJson(List<OdcmParameter> parameters) {
            if (!parameters.Any()) { return string.Empty; }

            var result = new StringBuilder();

            foreach (var param in parameters) {
                if (param.Type.GetTypeString() == "BOOL") {
                    result.AppendLine().Append("\t").AppendFormat("NSString *{0}String = [self.resolver.jsonSerializer serialize:({0} ? @\"true\" : @\"false\") property:@\"{1}\"];", param.Name.ToLowerFirstChar(), param.Name);
                } else if (param.Type.GetTypeString() == "int") {
                    result.AppendLine().Append("\t").AppendFormat("NSString *{0}String = [self.resolver.jsonSerializer serialize:[[NSString alloc] initWithFormat:@\"%d\", {0}],@\"{1}\"],", param.Name.ToLowerFirstChar(), param.Name);
                } else if (param.IsCollection) {
                    result.AppendLine().Append("\t")
                    .AppendFormat("NSString *{0}String = [self.resolver.jsonSerializer serialize:{0} property:@\"{1}\"];",
                    param.Name.ToLowerFirstChar(),
                    param.Name);
                } else {
                    result.AppendLine().Append("\t").AppendFormat("NSString *{0}String = [self.resolver.jsonSerializer serialize:{0} property:@\"{1}\"];", param.Name.ToLowerFirstChar(), param.Name);
                }
            }
            return result.ToString();
        }

        public string GetParametersForRawCall(IEnumerable<String> parameters) {
            //if(!parameters.Any()) { return string.Empty;}

            string result = "With";
            foreach (var param in parameters) {
                if (result == "With") {
                    result += string.Format("{0}:{1}String ", char.ToUpper(param[0]) + param.Substring(1)
                                        , param.ToLowerFirstChar());
                } else {
                    result += string.Format("{0}:{0}String ", param.ToLowerFirstChar());
                }
            }
            result += parameters.Count() > 0 ? "callback" : "Callback";
            return result;
        }

        public string GetFunctionParameters(List<OdcmParameter> parameters) {
            var result = new StringBuilder();
            //&&NSDictionary* params = [[NSDictionary alloc] initWithObjectsAndKeys :path,@"path",nil ];

            if (parameters.Any()) {
                result.Append("NSDictionary *params = [[NSDictionary alloc] initWithObjectsAndKeys:");
            } else {
                result.Append("NSDictionary *params = nil;");
            }
            foreach (var param in parameters) {

                if (param.Type.GetTypeString() == "bool") {
                    result.AppendFormat("{0} ? @\"true\" : @\"false\",", param.Name.ToLowerFirstChar());
                    result.AppendFormat("@\"{0}\"", param.Name);
                } else if (param.Type.GetTypeString() == "int") {
                    result.AppendFormat("[[NSString alloc] initWithFormat:@\"%d\", {0}],", param.Name.ToLowerFirstChar());
                    result.AppendFormat("@\"{0}\"", param.Name);
                } else {
                    result.AppendFormat("{0},", param.Name.ToLowerFirstChar());
                    result.AppendFormat("@\"{0}\",", param.Name);
                }

            }

            if (parameters.Any()) {
                result.AppendLine("nil];").AppendLine();
            }

            return result.ToString();
        }

        public string CreateEndOfFile(string name) {
            return string.Format("/n{0} EndOfFile", name);
        }

        public string GetParamsString(List<OdcmParameter> parameters) {

            string param = "With";

            foreach (OdcmParameter p in parameters) {
                if (param == "With") {
                    param += string.Format("{0}:({2}{3}){1} ", char.ToUpper(p.Name[0]) + p.Name.Substring(1)
                                        , p.Name.ToLowerFirstChar(),
                                        (p.IsCollection ?  "NSArray": p.Type.GetFullType())
                                        , (p.Type.IsComplex() ? " *" : ""));
                } else {
                    param += string.Format("{0}:({1} {2}){0} ", p.Name.ToLowerFirstChar(),
                    (p.IsCollection ?"NSArray" : p.Type.GetFullType())
                    , (p.Type.IsComplex() ? "*" : ""));
                }
            }
            param += parameters.Count() > 0 ? "callback" : "Callback";
            return param;
        }

        public string GetParamString(OdcmType p) {
            p.GetFullType();
            //if(p == null) return string.Empty;
            if (p.IsComplex()) {
                return p.IsSystem() ? string.Empty : p.GetTypeString() + " *" + p.Name.ToLowerFirstChar();
            }
            return p.GetTypeString() + " " + p.Name;
        }

        public string GetTypeForAction(OdcmMethod action) {
            if (action.ReturnType.IsComplex()) {
                if (action.IsCollection) {
                        return "NSArray *";
                }
                return action.ReturnType.IsSystem() ? action.ReturnType.GetTypeString() : action.ReturnType.GetTypeString() + " *";
            }
            return action.ReturnType.GetTypeString();
        }

        public string GetMethodHeader(OdcmMethod action) {
            var returnString = action.ReturnType == null ? "int returnValue"
            : GetTypeForAction(action) + action.ReturnType.Name.ToLowerFirstChar();
            return string.Format("- (void){0}{1}:(void (^)({2}, MSOrcError *error))callback",
            action.Name.ToLowerFirstChar(), GetParamsString(action.Parameters), returnString);
        }

        public string GetMethodHeaderRaw(OdcmMethod action) {
            return string.Format("- (void){0}Raw{1}:(void(^)(NSString *returnValue, MSOrcError *error))callback",
            action.Name.ToLowerFirstChar(),
            GetParamsForRaw(action.Parameters.Select(p => p.Name)), (action.ReturnType == null ? "NSString * resultCode " : GetParamRaw(action.ReturnType.Name)));
        }

        public string GetName(string name) {
            if (name.Trim() == "description") return "$$__$$description";
            if (name.Trim() == "default") return "$$__$$default";
            if (name.Trim() == "self") return "$$__$$self";
            return name;
        }
    }
}
