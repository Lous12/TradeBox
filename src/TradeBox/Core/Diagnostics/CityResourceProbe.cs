using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Lous12.TradeBox.Core.Diagnostics
{
    internal static class CityResourceProbe
    {
        private static readonly string[] ResourceProperties =
        {
            "amount_wood",
            "amount_stone",
            "amount_common_metals",
            "amount_gold"
        };

        private static readonly OpCode[] SingleByteOpCodes = new OpCode[256];
        private static readonly OpCode[] MultiByteOpCodes = new OpCode[256];

        static CityResourceProbe()
        {
            FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(OpCode))
                {
                    continue;
                }

                OpCode opCode = (OpCode)fields[i].GetValue(null);
                ushort value = unchecked((ushort)opCode.Value);
                if (value < 0x100)
                {
                    SingleByteOpCodes[value] = opCode;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    MultiByteOpCodes[value & 0xff] = opCode;
                }
            }
        }

        public static void DumpResourceApi(City city, Action<string> log)
        {
            if (city == null || log == null)
            {
                return;
            }

            Type cityType = city.GetType();
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.Static |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;

            log("[TradeBox-Probe] ===== RESOURCE API PROBE =====");
            log("[TradeBox-Probe] city type=" + cityType.FullName);

            for (int i = 0; i < ResourceProperties.Length; i++)
            {
                DumpProperty(cityType, ResourceProperties[i], flags, log);
            }

            DumpCandidateMethods(cityType, flags, log);
            DumpCandidateFields(cityType, flags, log);

            log("[TradeBox-Probe] ===== END RESOURCE API PROBE =====");
        }

        private static void DumpProperty(
            Type cityType,
            string propertyName,
            BindingFlags flags,
            Action<string> log
        )
        {
            PropertyInfo property = cityType.GetProperty(propertyName, flags);
            if (property == null)
            {
                log("[TradeBox-Probe] property " + propertyName + " = <not found>");
                return;
            }

            MethodInfo getter = property.GetGetMethod(true);
            MethodInfo setter = property.GetSetMethod(true);

            log(
                "[TradeBox-Probe] property " + cityType.Name + "." + propertyName +
                " type=" + TypeName(property.PropertyType) +
                " canRead=" + property.CanRead +
                " canWrite=" + property.CanWrite +
                " getter=" + MethodName(getter) +
                " setter=" + MethodName(setter)
            );

            if (getter != null)
            {
                DumpMethodIl(getter, log);
            }
        }

        private static void DumpMethodIl(MethodInfo method, Action<string> log)
        {
            MethodBody body;
            try
            {
                body = method.GetMethodBody();
            }
            catch (Exception exception)
            {
                log(
                    "[TradeBox-Probe]   IL " + MethodName(method) +
                    " = <GetMethodBody failed: " + exception.GetType().Name + ">"
                );
                return;
            }

            if (body == null)
            {
                log("[TradeBox-Probe]   IL " + MethodName(method) + " = <no body>");
                return;
            }

            byte[] il;
            try
            {
                il = body.GetILAsByteArray();
            }
            catch (Exception exception)
            {
                log(
                    "[TradeBox-Probe]   IL " + MethodName(method) +
                    " = <read failed: " + exception.GetType().Name + ">"
                );
                return;
            }

            if (il == null || il.Length == 0)
            {
                log("[TradeBox-Probe]   IL " + MethodName(method) + " = <empty>");
                return;
            }

            log(
                "[TradeBox-Probe]   IL " + MethodName(method) +
                " bytes=" + il.Length +
                " maxStack=" + body.MaxStackSize +
                " locals=" + body.LocalVariables.Count
            );

            int offset = 0;
            int instructionCount = 0;
            while (offset < il.Length && instructionCount < 200)
            {
                int instructionOffset = offset;
                OpCode opCode;

                byte first = il[offset++];
                if (first == 0xfe)
                {
                    if (offset >= il.Length)
                    {
                        log("[TradeBox-Probe]     IL_" + instructionOffset.ToString("X4") + ": <truncated opcode>");
                        break;
                    }
                    opCode = MultiByteOpCodes[il[offset++]];
                }
                else
                {
                    opCode = SingleByteOpCodes[first];
                }

                string operandText = string.Empty;
                int operandStart = offset;
                int operandSize;

                try
                {
                    operandSize = GetOperandSize(opCode.OperandType, il, offset);
                }
                catch
                {
                    log(
                        "[TradeBox-Probe]     IL_" + instructionOffset.ToString("X4") +
                        ": " + opCode.Name + " <operand parse failed>"
                    );
                    break;
                }

                if (operandSize < 0 || offset + operandSize > il.Length)
                {
                    log(
                        "[TradeBox-Probe]     IL_" + instructionOffset.ToString("X4") +
                        ": " + opCode.Name + " <truncated operand>"
                    );
                    break;
                }

                if (HasMetadataToken(opCode.OperandType) && operandSize >= 4)
                {
                    int token = BitConverter.ToInt32(il, operandStart);
                    operandText = ResolveToken(method, token, opCode.OperandType);
                }
                else if (opCode.OperandType == OperandType.ShortInlineI && operandSize == 1)
                {
                    operandText = il[operandStart].ToString();
                }
                else if (opCode.OperandType == OperandType.InlineI && operandSize == 4)
                {
                    operandText = BitConverter.ToInt32(il, operandStart).ToString();
                }

                log(
                    "[TradeBox-Probe]     IL_" + instructionOffset.ToString("X4") +
                    ": " + opCode.Name +
                    (string.IsNullOrEmpty(operandText) ? string.Empty : " " + operandText)
                );

                offset += operandSize;
                instructionCount++;
            }
        }

        private static int GetOperandSize(OperandType operandType, byte[] il, int offset)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (offset + 4 > il.Length)
                    {
                        return -1;
                    }
                    int count = BitConverter.ToInt32(il, offset);
                    if (count < 0)
                    {
                        return -1;
                    }
                    return 4 + (count * 4);
                default:
                    return 0;
            }
        }

        private static bool HasMetadataToken(OperandType operandType)
        {
            return operandType == OperandType.InlineField ||
                   operandType == OperandType.InlineMethod ||
                   operandType == OperandType.InlineTok ||
                   operandType == OperandType.InlineType ||
                   operandType == OperandType.InlineString ||
                   operandType == OperandType.InlineSig;
        }

        private static string ResolveToken(
            MethodInfo context,
            int token,
            OperandType operandType
        )
        {
            Module module = context.Module;

            try
            {
                if (operandType == OperandType.InlineString)
                {
                    return "\"" + module.ResolveString(token) + "\"";
                }

                if (operandType == OperandType.InlineSig)
                {
                    return "token=0x" + token.ToString("X8") + " <signature>";
                }

                Type[] typeArguments = null;
                Type declaringType = context.DeclaringType;
                if (declaringType != null && declaringType.IsGenericType)
                {
                    typeArguments = declaringType.GetGenericArguments();
                }

                Type[] methodArguments = null;
                if (context.IsGenericMethod)
                {
                    methodArguments = context.GetGenericArguments();
                }

                MemberInfo member = module.ResolveMember(token, typeArguments, methodArguments);
                return "token=0x" + token.ToString("X8") + " -> " + DescribeMember(member);
            }
            catch (Exception exception)
            {
                return "token=0x" + token.ToString("X8") +
                       " <resolve failed: " + exception.GetType().Name + ">";
            }
        }

        private static void DumpCandidateMethods(
            Type cityType,
            BindingFlags flags,
            Action<string> log
        )
        {
            log("[TradeBox-Probe] -- candidate City resource/storage methods --");

            MethodInfo[] methods;
            try
            {
                methods = cityType.GetMethods(flags);
            }
            catch
            {
                methods = new MethodInfo[0];
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.IsSpecialName || !LooksResourceRelated(method.Name))
                {
                    continue;
                }

                lines.Add(DescribeMethod(method));
            }

            lines.Sort(StringComparer.Ordinal);
            if (lines.Count == 0)
            {
                log("[TradeBox-Probe]   <none>");
                return;
            }

            for (int i = 0; i < lines.Count && i < 120; i++)
            {
                log("[TradeBox-Probe]   " + lines[i]);
            }
        }

        private static void DumpCandidateFields(
            Type cityType,
            BindingFlags flags,
            Action<string> log
        )
        {
            log("[TradeBox-Probe] -- candidate City resource/storage fields --");

            FieldInfo[] fields;
            try
            {
                fields = cityType.GetFields(flags);
            }
            catch
            {
                fields = new FieldInfo[0];
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || !LooksResourceRelated(field.Name))
                {
                    continue;
                }

                lines.Add(
                    (field.IsStatic ? "static " : "instance ") +
                    TypeName(field.FieldType) + " " + field.Name
                );
            }

            lines.Sort(StringComparer.Ordinal);
            if (lines.Count == 0)
            {
                log("[TradeBox-Probe]   <none>");
                return;
            }

            for (int i = 0; i < lines.Count && i < 120; i++)
            {
                log("[TradeBox-Probe]   " + lines[i]);
            }
        }

        private static bool LooksResourceRelated(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("resource") ||
                   lower.Contains("storage") ||
                   lower.Contains("stock") ||
                   lower.Contains("food") ||
                   lower.Contains("wood") ||
                   lower.Contains("stone") ||
                   lower.Contains("metal") ||
                   lower.Contains("gold") ||
                   lower.Contains("item");
        }

        private static string DescribeMember(MemberInfo member)
        {
            if (member == null)
            {
                return "<null member>";
            }

            FieldInfo field = member as FieldInfo;
            if (field != null)
            {
                return TypeName(field.DeclaringType) + "." + field.Name +
                       " : " + TypeName(field.FieldType);
            }

            MethodInfo method = member as MethodInfo;
            if (method != null)
            {
                return DescribeMethod(method);
            }

            ConstructorInfo constructor = member as ConstructorInfo;
            if (constructor != null)
            {
                return DescribeMethod(constructor);
            }

            Type type = member as Type;
            if (type != null)
            {
                return "type " + TypeName(type);
            }

            return TypeName(member.DeclaringType) + "." + member.Name +
                   " [" + member.MemberType + "]";
        }

        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
            {
                return "<null method>";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(method.IsStatic ? "static " : "instance ");

            MethodInfo methodInfo = method as MethodInfo;
            if (methodInfo != null)
            {
                builder.Append(TypeName(methodInfo.ReturnType));
                builder.Append(" ");
            }

            builder.Append(TypeName(method.DeclaringType));
            builder.Append(".");
            builder.Append(method.Name);
            builder.Append("(");

            ParameterInfo[] parameters;
            try
            {
                parameters = method.GetParameters();
            }
            catch
            {
                parameters = new ParameterInfo[0];
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(TypeName(parameters[i].ParameterType));
                if (!string.IsNullOrEmpty(parameters[i].Name))
                {
                    builder.Append(" ");
                    builder.Append(parameters[i].Name);
                }
            }

            builder.Append(")");
            return builder.ToString();
        }

        private static string MethodName(MethodInfo method)
        {
            if (method == null)
            {
                return "<none>";
            }

            return TypeName(method.DeclaringType) + "." + method.Name;
        }

        private static string TypeName(Type type)
        {
            if (type == null)
            {
                return "<null>";
            }

            return string.IsNullOrEmpty(type.FullName) ? type.Name : type.FullName;
        }
    }
}
