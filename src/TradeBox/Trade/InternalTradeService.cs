using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Lous12.TradeBox.Data;
using Lous12.TradeBox.Economy;
using Lous12.TradeBox.Resources;
using UnityEngine;

namespace Lous12.TradeBox.Trade
{
    public sealed class InternalTradeService
    {
        private readonly CityEconomyService _economies;

        public InternalTradeService(CityEconomyService economies)
        {
            _economies = economies;
        }

        public bool TryExecuteOneTrade(IList<City> cities, Action<string> log)
        {
            if (cities == null || cities.Count < 2)
            {
                return false;
            }

            for (int targetIndex = 0; targetIndex < cities.Count; targetIndex++)
            {
                City target = cities[targetIndex];
                CityEconomy targetEconomy = _economies.GetOrCreate(target);
                if (targetEconomy == null)
                {
                    continue;
                }

                for (int resourceIndex = 0; resourceIndex < VanillaResourceIds.Core.Length; resourceIndex++)
                {
                    string resourceId = VanillaResourceIds.Core[resourceIndex];
                    ResourceBalance targetBalance = targetEconomy.GetOrCreateResource(resourceId);
                    if (targetBalance.Deficit <= 0)
                    {
                        continue;
                    }

                    City source = FindNearestSource(cities, target, resourceId);
                    if (source == null)
                    {
                        continue;
                    }

                    CityEconomy sourceEconomy = _economies.GetOrCreate(source);
                    ResourceBalance sourceBalance = sourceEconomy.GetOrCreateResource(resourceId);
                    int amount = Math.Min(sourceBalance.Surplus, targetBalance.Deficit);
                    if (amount <= 0)
                    {
                        continue;
                    }

                    TradeTransferResult result;
                    if (!TryTransfer(source, target, resourceId, amount, out result))
                    {
                        log(
                            "[TradeBox-Trade] transfer failed resource=" + resourceId +
                            " amount=" + amount +
                            " source=" + sourceEconomy.CityId +
                            " target=" + targetEconomy.CityId
                        );
                        continue;
                    }

                    _economies.RegisterTradeDelta(source, resourceId, -amount);
                    _economies.RegisterTradeDelta(target, resourceId, amount);

                    TradeBoxState.RecentTransactions.Add(new TradeTransaction
                    {
                        SourceCityId = sourceEconomy.CityId,
                        SourceCityName = GetCityDisplayName(source),
                        TargetCityId = targetEconomy.CityId,
                        TargetCityName = GetCityDisplayName(target),
                        ResourceId = resourceId,
                        Amount = amount,
                        WorldYear = 0
                    });

                    if (TradeBoxState.RecentTransactions.Count > 64)
                    {
                        TradeBoxState.RecentTransactions.RemoveAt(0);
                    }

                    float distance;
                    string distanceText = TryGetDistance(source, target, out distance)
                        ? distance.ToString("0.0")
                        : "n/a";

                    log(
                        "[TradeBox-Trade] EXECUTED resource=" + resourceId +
                        " amount=" + amount +
                        " source=" + sourceEconomy.CityId +
                        " target=" + targetEconomy.CityId +
                        " distance=" + distanceText +
                        " source=" + result.SourceBefore + "->" + result.SourceAfter +
                        " target=" + result.TargetBefore + "->" + result.TargetAfter
                    );
                    return true;
                }
            }

            log("[TradeBox-Trade] No eligible same-kingdom surplus->deficit pair this cycle.");
            return false;
        }

        public bool RunControlledRoundTripProbe(IList<City> cities, Action<string> log)
        {
            log("[TradeBox-Trade] ===== INTERNAL TRADE ROUNDTRIP PROBE =====");

            if (cities == null || cities.Count < 2)
            {
                log("[TradeBox-Trade] RESULT=INCONCLUSIVE fewer than two cities");
                log("[TradeBox-Trade] ===== END INTERNAL TRADE ROUNDTRIP PROBE =====");
                return false;
            }

            for (int i = 0; i < cities.Count; i++)
            {
                City source = cities[i];
                for (int j = 0; j < cities.Count; j++)
                {
                    City target = cities[j];
                    if (object.ReferenceEquals(source, target) || !SameKingdom(source, target))
                    {
                        continue;
                    }

                    for (int r = 0; r < VanillaResourceIds.Core.Length; r++)
                    {
                        string resourceId = VanillaResourceIds.Core[r];
                        if (VanillaResourceAdapter.GetAmount(source, resourceId) <= 0)
                        {
                            continue;
                        }

                        int sourceBefore = VanillaResourceAdapter.GetAmount(source, resourceId);
                        int targetBefore = VanillaResourceAdapter.GetAmount(target, resourceId);
                        int sourceVersionBefore = VanillaResourceAdapter.GetStorageVersion(source);
                        int targetVersionBefore = VanillaResourceAdapter.GetStorageVersion(target);

                        TradeTransferResult forward;
                        if (!TryTransfer(source, target, resourceId, 1, out forward))
                        {
                            continue;
                        }

                        TradeTransferResult reverse;
                        if (!TryTransfer(target, source, resourceId, 1, out reverse))
                        {
                            // Best-effort restore if reverse transfer failed.
                            if (VanillaResourceAdapter.GetAmount(target, resourceId) > targetBefore)
                            {
                                VanillaResourceAdapter.TryTake(target, resourceId, 1);
                                VanillaResourceAdapter.Add(source, resourceId, 1);
                            }

                            log("[TradeBox-Trade] RESULT=FAIL reverse transfer failed");
                            log("[TradeBox-Trade] ===== END INTERNAL TRADE ROUNDTRIP PROBE =====");
                            return false;
                        }

                        int sourceFinal = VanillaResourceAdapter.GetAmount(source, resourceId);
                        int targetFinal = VanillaResourceAdapter.GetAmount(target, resourceId);
                        int sourceVersionAfter = VanillaResourceAdapter.GetStorageVersion(source);
                        int targetVersionAfter = VanillaResourceAdapter.GetStorageVersion(target);

                        CityEconomy sourceEconomy = _economies.GetOrCreate(source);
                        CityEconomy targetEconomy = _economies.GetOrCreate(target);
                        float distance;
                        string distanceText = TryGetDistance(source, target, out distance)
                            ? distance.ToString("0.0")
                            : "n/a";

                        log(
                            "[TradeBox-Trade] pair source=" + sourceEconomy.CityId +
                            " target=" + targetEconomy.CityId +
                            " sameKingdom=true distance=" + distanceText +
                            " resource=" + resourceId
                        );
                        log(
                            "[TradeBox-Trade] forward source=" + sourceBefore + "->" + forward.SourceAfter +
                            " target=" + targetBefore + "->" + forward.TargetAfter
                        );
                        log(
                            "[TradeBox-Trade] restored source=" + sourceFinal +
                            " target=" + targetFinal +
                            " storageVersion source=" + sourceVersionBefore + "->" + sourceVersionAfter +
                            " target=" + targetVersionBefore + "->" + targetVersionAfter
                        );

                        bool pass = sourceFinal == sourceBefore && targetFinal == targetBefore;
                        log(
                            pass
                                ? "[TradeBox-Trade] RESULT=PASS same-kingdom transfer pipeline restored original stocks"
                                : "[TradeBox-Trade] RESULT=FAIL roundtrip did not restore original stocks"
                        );
                        log("[TradeBox-Trade] ===== END INTERNAL TRADE ROUNDTRIP PROBE =====");
                        return pass;
                    }
                }
            }

            log("[TradeBox-Trade] RESULT=INCONCLUSIVE no same-kingdom pair with transferable core resource");
            log("[TradeBox-Trade] ===== END INTERNAL TRADE ROUNDTRIP PROBE =====");
            return false;
        }

        public static List<City> FindAllCities(object world)
        {
            List<City> cities = new List<City>();
            if (world == null)
            {
                return cities;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type worldType = world.GetType();

            FieldInfo[] fields;
            try { fields = worldType.GetFields(flags); }
            catch { fields = new FieldInfo[0]; }

            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try { value = fields[i].GetValue(world); }
                catch { continue; }
                CollectCities(value, cities, 0);
            }

            PropertyInfo[] properties;
            try { properties = worldType.GetProperties(flags); }
            catch { properties = new PropertyInfo[0]; }

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object value;
                try { value = property.GetValue(world, null); }
                catch { continue; }
                CollectCities(value, cities, 0);
            }

            return cities;
        }

        public static List<City> FilterKingdomBoundCities(IList<City> cities)
        {
            List<City> filtered = new List<City>();
            if (cities == null)
            {
                return filtered;
            }

            for (int i = 0; i < cities.Count; i++)
            {
                City city = cities[i];
                if (city == null || GetKingdomKey(city) == null)
                {
                    continue;
                }

                AddUnique(filtered, city);
            }

            return filtered;
        }

        public static string GetCityDisplayName(City city)
        {
            if (city == null)
            {
                return "?";
            }

            string name = TryGetStableText(city, new[] { "name", "city_name", "cityName" });
            object data = GetNamedMember(city, new[] { "data", "_data" });
            if (string.IsNullOrEmpty(name) && data != null)
            {
                name = TryGetStableText(data, new[] { "name", "city_name", "cityName" });
            }

            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            return "City";
        }

        public static string DescribeCity(City city)
        {
            if (city == null)
            {
                return "<null>";
            }

            string runtimeId = "runtime:" + RuntimeHelpers.GetHashCode(city).ToString("X8");
            string stableId = TryGetStableText(city, new[] { "id", "city_id", "cityID", "cityId" });
            string name = TryGetStableText(city, new[] { "name", "city_name", "cityName" });
            object data = GetNamedMember(city, new[] { "data", "_data" });
            if (string.IsNullOrEmpty(stableId) && data != null)
            {
                stableId = TryGetStableText(data, new[] { "id", "city_id", "cityID", "cityId" });
            }
            if (string.IsNullOrEmpty(name) && data != null)
            {
                name = TryGetStableText(data, new[] { "name", "city_name", "cityName" });
            }

            float x;
            float y;
            string pos = TryGetCoordinates(city, out x, out y)
                ? x.ToString("0.0") + "," + y.ToString("0.0")
                : "n/a";

            return runtimeId +
                " stableId=" + (string.IsNullOrEmpty(stableId) ? "n/a" : stableId) +
                " name=" + (string.IsNullOrEmpty(name) ? "n/a" : name) +
                " kingdom=" + (GetKingdomKey(city) == null ? "null" : "set") +
                " pos=" + pos;
        }

        private City FindNearestSource(IList<City> cities, City target, string resourceId)
        {
            City best = null;
            float bestDistance = float.MaxValue;
            bool hasMeasuredDistance = false;

            for (int i = 0; i < cities.Count; i++)
            {
                City candidate = cities[i];
                if (candidate == null || object.ReferenceEquals(candidate, target) || !SameKingdom(candidate, target))
                {
                    continue;
                }

                CityEconomy economy = _economies.GetOrCreate(candidate);
                if (economy == null || economy.GetOrCreateResource(resourceId).Surplus <= 0)
                {
                    continue;
                }

                float distance;
                if (TryGetDistance(candidate, target, out distance))
                {
                    if (!hasMeasuredDistance || distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                        hasMeasuredDistance = true;
                    }
                }
                else if (!hasMeasuredDistance && best == null)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool TryTransfer(
            City source,
            City target,
            string resourceId,
            int amount,
            out TradeTransferResult result
        )
        {
            result = new TradeTransferResult();
            if (source == null || target == null || amount <= 0 || string.IsNullOrEmpty(resourceId))
            {
                return false;
            }

            result.SourceBefore = VanillaResourceAdapter.GetAmount(source, resourceId);
            result.TargetBefore = VanillaResourceAdapter.GetAmount(target, resourceId);
            if (result.SourceBefore < amount)
            {
                return false;
            }

            if (!VanillaResourceAdapter.TryTake(source, resourceId, amount))
            {
                return false;
            }

            result.SourceAfter = VanillaResourceAdapter.GetAmount(source, resourceId);
            VanillaResourceAdapter.Add(target, resourceId, amount);
            result.TargetAfter = VanillaResourceAdapter.GetAmount(target, resourceId);

            if (result.TargetAfter == result.TargetBefore + amount)
            {
                return true;
            }

            int partialAdded = result.TargetAfter - result.TargetBefore;
            if (partialAdded > 0)
            {
                VanillaResourceAdapter.TryTake(target, resourceId, partialAdded);
            }

            VanillaResourceAdapter.Add(source, resourceId, amount);
            result.SourceAfter = VanillaResourceAdapter.GetAmount(source, resourceId);
            result.TargetAfter = VanillaResourceAdapter.GetAmount(target, resourceId);
            return false;
        }

        private static bool SameKingdom(City first, City second)
        {
            object firstKey = GetKingdomKey(first);
            object secondKey = GetKingdomKey(second);
            if (firstKey == null || secondKey == null)
            {
                return false;
            }

            if (firstKey.GetType().IsValueType || firstKey is string || secondKey.GetType().IsValueType || secondKey is string)
            {
                return string.Equals(firstKey.ToString(), secondKey.ToString(), StringComparison.Ordinal);
            }

            return object.ReferenceEquals(firstKey, secondKey);
        }

        private static object GetKingdomKey(City city)
        {
            if (city == null)
            {
                return null;
            }

            object value = GetNamedMember(city, new[] { "kingdom", "_kingdom", "kingdom_id", "kingdomID" });
            if (value != null)
            {
                return value;
            }

            value = InvokeNamedMethod(city, new[] { "getKingdom", "getKingdomID", "getKingdomId" });
            if (value != null)
            {
                return value;
            }

            object data = GetNamedMember(city, new[] { "data", "_data" });
            if (data != null)
            {
                value = GetNamedMember(data, new[] { "kingdom", "_kingdom", "kingdom_id", "kingdomID", "kingdomId" });
                if (value != null)
                {
                    return value;
                }
            }

            return GetFirstMemberContaining(city, "kingdom");
        }

        private static bool TryGetDistance(City first, City second, out float distance)
        {
            distance = 0f;
            float ax;
            float ay;
            float bx;
            float by;
            if (!TryGetCoordinates(first, out ax, out ay) || !TryGetCoordinates(second, out bx, out by))
            {
                return false;
            }

            float dx = ax - bx;
            float dy = ay - by;
            distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return true;
        }

        private static bool TryGetCoordinates(object source, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (source == null)
            {
                return false;
            }

            Vector2 vector2;
            if (source is Vector2)
            {
                vector2 = (Vector2)source;
                x = vector2.x;
                y = vector2.y;
                return true;
            }

            Vector3 vector3;
            if (source is Vector3)
            {
                vector3 = (Vector3)source;
                x = vector3.x;
                y = vector3.y;
                return true;
            }

            object nested = InvokeNamedMethod(source, new[] { "getCenterTile", "getTile", "getCenter", "getPosition" });
            if (nested != null && !object.ReferenceEquals(nested, source) && TryGetCoordinatesFromMembers(nested, out x, out y))
            {
                return true;
            }

            nested = GetNamedMember(source, new[] { "centerTile", "center_tile", "tile", "currentTile", "current_tile", "position", "pos", "center", "location" });
            if (nested != null && !object.ReferenceEquals(nested, source) && TryGetCoordinatesFromMembers(nested, out x, out y))
            {
                return true;
            }

            if (TryGetCoordinatesFromMembers(source, out x, out y))
            {
                return true;
            }

            return TryGetCoordinatesFromLikelyMembers(source, out x, out y);
        }

        private static bool TryGetCoordinatesFromLikelyMembers(object source, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (source == null)
            {
                return false;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = source.GetType();
            string[] fragments = { "tile", "pos", "center", "location", "zone" };

            FieldInfo[] fields;
            try { fields = type.GetFields(flags); }
            catch { fields = new FieldInfo[0]; }

            for (int i = 0; i < fields.Length; i++)
            {
                if (!ContainsAny(fields[i].Name, fragments))
                {
                    continue;
                }
                object value;
                try { value = fields[i].GetValue(source); }
                catch { continue; }
                if (value != null && !object.ReferenceEquals(value, source) && TryGetCoordinatesFromMembers(value, out x, out y))
                {
                    return true;
                }
            }

            PropertyInfo[] properties;
            try { properties = type.GetProperties(flags); }
            catch { properties = new PropertyInfo[0]; }

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !ContainsAny(property.Name, fragments))
                {
                    continue;
                }
                object value;
                try { value = property.GetValue(source, null); }
                catch { continue; }
                if (value != null && !object.ReferenceEquals(value, source) && TryGetCoordinatesFromMembers(value, out x, out y))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAny(string text, string[] fragments)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            for (int i = 0; i < fragments.Length; i++)
            {
                if (text.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetCoordinatesFromMembers(object source, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (source == null)
            {
                return false;
            }

            object xValue = GetNamedMember(source, new[] { "x", "X", "posX", "pos_x" });
            object yValue = GetNamedMember(source, new[] { "y", "Y", "posY", "pos_y" });
            if (TryConvertFloat(xValue, out x) && TryConvertFloat(yValue, out y))
            {
                return true;
            }

            object nested = GetNamedMember(source, new[] { "pos", "position", "tilePos", "tile_pos" });
            if (nested is Vector2)
            {
                Vector2 v2 = (Vector2)nested;
                x = v2.x;
                y = v2.y;
                return true;
            }
            if (nested is Vector3)
            {
                Vector3 v3 = (Vector3)nested;
                x = v3.x;
                y = v3.y;
                return true;
            }

            return false;
        }

        private static bool TryConvertFloat(object value, out float result)
        {
            result = 0f;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToSingle(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string TryGetStableText(object instance, string[] names)
        {
            object value = GetNamedMember(instance, names);
            if (value == null)
            {
                return null;
            }
            try
            {
                string text = value.ToString();
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }

        private static object GetNamedMember(object instance, string[] names)
        {
            if (instance == null)
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();

            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field = type.GetField(names[i], flags);
                if (field != null)
                {
                    try { return field.GetValue(instance); }
                    catch { }
                }

                PropertyInfo property = type.GetProperty(names[i], flags);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    try { return property.GetValue(instance, null); }
                    catch { }
                }
            }

            return null;
        }

        private static object InvokeNamedMethod(object instance, string[] names)
        {
            if (instance == null)
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo method = type.GetMethod(names[i], flags, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    continue;
                }

                try { return method.Invoke(instance, null); }
                catch { }
            }

            return null;
        }

        private static object GetFirstMemberContaining(object instance, string fragment)
        {
            if (instance == null)
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();

            FieldInfo[] fields;
            try { fields = type.GetFields(flags); }
            catch { fields = new FieldInfo[0]; }

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                try
                {
                    object value = fields[i].GetValue(instance);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch { }
            }

            PropertyInfo[] properties;
            try { properties = type.GetProperties(flags); }
            catch { properties = new PropertyInfo[0]; }

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0 ||
                    !property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }
                try
                {
                    object value = property.GetValue(instance, null);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch { }
            }

            return null;
        }

        private static void CollectCities(object candidate, List<City> cities, int depth)
        {
            if (candidate == null || depth > 2)
            {
                return;
            }

            City direct = candidate as City;
            if (direct != null)
            {
                AddUnique(cities, direct);
                return;
            }

            IDictionary dictionary = candidate as IDictionary;
            if (dictionary != null)
            {
                foreach (object value in dictionary.Values)
                {
                    City city = value as City;
                    if (city != null)
                    {
                        AddUnique(cities, city);
                    }
                }
                return;
            }

            IEnumerable enumerable = candidate as IEnumerable;
            if (enumerable != null && !(candidate is string))
            {
                try
                {
                    foreach (object value in enumerable)
                    {
                        City city = value as City;
                        if (city != null)
                        {
                            AddUnique(cities, city);
                        }
                    }
                }
                catch { }
            }

            Type type = candidate.GetType();
            string typeName = type.Name ?? string.Empty;
            if (typeName.IndexOf("City", StringComparison.OrdinalIgnoreCase) < 0 ||
                typeName == typeof(City).Name)
            {
                return;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields;
            try { fields = type.GetFields(flags); }
            catch { fields = new FieldInfo[0]; }

            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try { value = fields[i].GetValue(candidate); }
                catch { continue; }
                CollectCities(value, cities, depth + 1);
            }
        }

        private static void AddUnique(List<City> cities, City city)
        {
            for (int i = 0; i < cities.Count; i++)
            {
                if (object.ReferenceEquals(cities[i], city))
                {
                    return;
                }
            }
            cities.Add(city);
        }

        private struct TradeTransferResult
        {
            public int SourceBefore;
            public int SourceAfter;
            public int TargetBefore;
            public int TargetAfter;
        }
    }
}
