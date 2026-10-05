using System;
using System.Reflection;

namespace Lous12.TradeBox.Core.Diagnostics
{
    internal static class CityResourceMutationProbe
    {
        private const string TestResourceId = "wood";
        private const int TestAmount = 1;

        public static void Run(City city, Action<string> log)
        {
            if (city == null || log == null)
            {
                return;
            }

            Type cityType = city.GetType();
            BindingFlags flags = BindingFlags.Instance |
                                 BindingFlags.Public |
                                 BindingFlags.NonPublic;

            MethodInfo getAmount = FindMethod(
                cityType,
                "getResourcesAmount",
                flags,
                typeof(string)
            );
            MethodInfo addResource = FindMethod(
                cityType,
                "addResourcesToRandomStockpile",
                flags,
                typeof(string),
                typeof(int)
            );
            MethodInfo takeResource = FindMethod(
                cityType,
                "takeResource",
                flags,
                typeof(string),
                typeof(int)
            );
            MethodInfo getStorageVersion = FindMethod(
                cityType,
                "getStorageVersion",
                flags
            );

            log("[TradeBox-Probe] ===== RESOURCE MUTATION PROBE =====");
            log("[TradeBox-Probe] resource=" + TestResourceId + " amount=" + TestAmount);
            LogMethod("read", getAmount, log);
            LogMethod("add", addResource, log);
            LogMethod("take", takeResource, log);
            LogMethod("storageVersion", getStorageVersion, log);

            if (getAmount == null || addResource == null || takeResource == null)
            {
                log("[TradeBox-Probe] RESULT=INCONCLUSIVE required vanilla method missing");
                log("[TradeBox-Probe] ===== END RESOURCE MUTATION PROBE =====");
                return;
            }

            int before = InvokeInt(getAmount, city, new object[] { TestResourceId });
            int versionBefore = InvokeOptionalInt(getStorageVersion, city);
            log(
                "[TradeBox-Probe] BEFORE amount=" + before +
                " storageVersion=" + FormatOptional(versionBefore)
            );

            object addResultObject = addResource.Invoke(
                city,
                new object[] { TestResourceId, TestAmount }
            );
            int addResult = Convert.ToInt32(addResultObject);
            int afterAdd = InvokeInt(getAmount, city, new object[] { TestResourceId });
            int versionAfterAdd = InvokeOptionalInt(getStorageVersion, city);

            log(
                "[TradeBox-Probe] AFTER_ADD return=" + addResult +
                " amount=" + afterAdd +
                " delta=" + (afterAdd - before) +
                " storageVersion=" + FormatOptional(versionAfterAdd)
            );

            if (afterAdd != before + TestAmount)
            {
                log(
                    "[TradeBox-Probe] RESULT=INCONCLUSIVE add did not produce expected +" +
                    TestAmount + "; takeResource was NOT called"
                );
                log("[TradeBox-Probe] ===== END RESOURCE MUTATION PROBE =====");
                return;
            }

            takeResource.Invoke(
                city,
                new object[] { TestResourceId, TestAmount }
            );

            int afterTake = InvokeInt(getAmount, city, new object[] { TestResourceId });
            int versionAfterTake = InvokeOptionalInt(getStorageVersion, city);
            log(
                "[TradeBox-Probe] AFTER_TAKE amount=" + afterTake +
                " deltaFromBefore=" + (afterTake - before) +
                " storageVersion=" + FormatOptional(versionAfterTake)
            );

            if (afterTake == before)
            {
                log("[TradeBox-Probe] RESULT=PASS vanilla add/read/take round-trip restored original amount");
            }
            else
            {
                log(
                    "[TradeBox-Probe] RESULT=FAIL round-trip did not restore original amount; " +
                    "before=" + before + " final=" + afterTake
                );
            }

            log("[TradeBox-Probe] ===== END RESOURCE MUTATION PROBE =====");
        }

        private static MethodInfo FindMethod(
            Type type,
            string name,
            BindingFlags flags,
            params Type[] parameterTypes
        )
        {
            try
            {
                return type.GetMethod(
                    name,
                    flags,
                    null,
                    parameterTypes,
                    null
                );
            }
            catch
            {
                return null;
            }
        }

        private static int InvokeInt(MethodInfo method, object instance, object[] arguments)
        {
            object result = method.Invoke(instance, arguments);
            return Convert.ToInt32(result);
        }

        private static int InvokeOptionalInt(MethodInfo method, object instance)
        {
            if (method == null)
            {
                return int.MinValue;
            }

            try
            {
                object result = method.Invoke(instance, null);
                return Convert.ToInt32(result);
            }
            catch
            {
                return int.MinValue;
            }
        }

        private static string FormatOptional(int value)
        {
            return value == int.MinValue ? "<unavailable>" : value.ToString();
        }

        private static void LogMethod(string label, MethodInfo method, Action<string> log)
        {
            if (method == null)
            {
                log("[TradeBox-Probe] method " + label + "=<not found>");
                return;
            }

            string visibility = method.IsPublic
                ? "public"
                : method.IsPrivate
                    ? "private"
                    : method.IsFamily
                        ? "protected"
                        : method.IsAssembly
                            ? "internal"
                            : "nonpublic";

            log(
                "[TradeBox-Probe] method " + label + "=" +
                method.DeclaringType.Name + "." + method.Name +
                " visibility=" + visibility +
                " return=" + method.ReturnType.FullName
            );
        }
    }
}
