using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Client.Network.Escrow;
using Game.Shared;
using Game.Shared.Domain;
using Reckoning.Game;
using UnityEngine;

namespace HexRevival
{
    /// <summary>
    /// PvE full sets (an "additional items in the store" product, Store.ExtraItems; Criesener's idea, user 2026-11-02): next to each set's Full Set
    /// (one of every PvP card), a store package with everything of the set's PvE side that otherwise comes from its
    /// chests and Wheel of Fate, for players who would rather not open and spin chests:
    /// - the set's PvE cards (its "Set N Chest Loot" and "Set N Wheels of Fate" cards, Src.Zymmer), one copy each (4 for
    ///   the x4 variants, Store.PveCopiesOf), never beyond 4 owned;
    /// - every piece of its chest and wheel equipment (Src.Zymmer), the ones not owned yet;
    /// - its six mercenaries, three from the chests and three from the wheel (Mercenaries.OfSet), not owned yet;
    /// - its sleeves "Found in Chests for [set]" and "Found in the Wheels of Fate for [set]" (Src.HexCodex), not owned yet.
    ///
    /// The client keys store products by their item (UIPremiumStoreViewModel.AddItemIfItDoesNotExist), so each package
    /// is an item of its own: a copy of the set's Full Set item with a fixed id of the mod and its own name, registered in
    /// the TemplateManager when the store is built. Price PLACEHOLDER (see GoldPrice).
    /// </summary>
    public static class PveFullSets
    {
        /// <summary>PLACEHOLDER gold price (written as the other platinum-era products: converted by the account's price settings).</summary>
        public const int GoldPrice = 100000;

        /// <summary>The packages registered: their item id -> the card set.</summary>
        private static readonly Dictionary<ResourceId, CardSetTemplate> s_Sets = new Dictionary<ResourceId, CardSetTemplate>();

        /// <summary>The current account's "additional items in the store" option.</summary>
        public static bool Enabled { get { return Store.ExtraItems; } }

        /// <summary>The set of a package item (null: not one).</summary>
        public static CardSetTemplate SetOf(InventoryItemData item)
        {
            CardSetTemplate s;
            return item != null && s_Sets.TryGetValue(item.m_Id, out s) ? s : null;
        }

        /// <summary>The package of a set's Full Set item (null: none, or the option is off).</summary>
        public static InventoryItemData ForFullSet(InventoryItemData fullSet)
        {
            InventoryCardPack p = fullSet as InventoryCardPack;
            CardSetTemplate set;
            if (!Enabled || p == null || p.m_CardPackType != ECardPackType.FullSet || s_Sets.ContainsKey(p.m_Id)
                || !Singleton<TemplateManager>.Instance.CardSets.TryGetValue(p.m_SetId, out set) || set == null) return null;
            ResourceId id = IdFor(set);
            return s_Sets.ContainsKey(id) ? Singleton<TemplateManager>.Instance.InventoryItems[id] : null;
        }

        /// <summary>
        /// The packages' items, made once from the sets' Full Set items (only for the 9 booster sets, whose chests and
        /// wheel the notes list). Returned in the sets' order.
        /// </summary>
        public static List<InventoryItemData> Items()
        {
            TemplateManager tm = Singleton<TemplateManager>.Instance;
            List<InventoryItemData> fullSets = new List<InventoryItemData>();
            foreach (InventoryItemData i in tm.InventoryItems.Values)
            {
                InventoryCardPack p = i as InventoryCardPack;
                if (p != null && p.m_CardPackType == ECardPackType.FullSet && !s_Sets.ContainsKey(p.m_Id)) fullSets.Add(p);
            }
            List<InventoryItemData> res = new List<InventoryItemData>();
            foreach (InventoryItemData i in fullSets)
            {
                InventoryCardPack src = (InventoryCardPack)i;
                CardSetTemplate set;
                if (!tm.CardSets.TryGetValue(src.m_SetId, out set) || set == null || Zymmer.SetNumber(set) <= 0) continue;
                ResourceId id = IdFor(set);
                InventoryItemData made;
                if (tm.InventoryItems.ContainsKey(id)) made = tm.InventoryItems[id];
                else
                {
                    made = Copy(src);
                    made.m_Id = id;
                    made.m_Name = Loc.Get("pve_fullset_name", set.m_Name.Trim());
                    if (!Register(tm, made)) continue;
                }
                s_Sets[id] = set;
                if (Count(set) > 0) res.Add(made);
            }
            res.Sort((a, b) => Zymmer.SetNumber(s_Sets[a.m_Id]).CompareTo(Zymmer.SetNumber(s_Sets[b.m_Id])));
            return res;
        }

        /// <summary>Adds an item to the game's items (TemplateLookup's dictionary m_Lookup, read-only from outside).</summary>
        private static bool Register(TemplateManager tm, InventoryItemData item)
        {
            try
            {
                object lookup = tm.InventoryItems;
                System.Collections.IDictionary d = lookup.GetType().GetField("m_Lookup", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(lookup) as System.Collections.IDictionary;
                if (d == null) return false;
                d[item.m_Id] = item;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[HexRevival] [PveFullSets] " + e.Message); return false; }
        }

        /// <summary>A fixed id per set (the same at every start): a name-based GUID of the set's id.</summary>
        private static ResourceId IdFor(CardSetTemplate set)
        {
            byte[] h;
            using (var md5 = System.Security.Cryptography.MD5.Create())
                h = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("HexRevival.PveFullSet:" + set.m_Id));
            h[6] = (byte)((h[6] & 0x0F) | 0x30);
            h[8] = (byte)((h[8] & 0x3F) | 0x80);
            return new ResourceId(new Guid(h));
        }

        /// <summary>A copy of the item (every field, the base classes' too).</summary>
        private static InventoryCardPack Copy(InventoryCardPack src)
        {
            InventoryCardPack c = new InventoryCardPack();
            for (Type t = typeof(InventoryCardPack); t != null && t != typeof(object); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    f.SetValue(c, f.GetValue(src));
            return c;
        }

        // ------------------------------------------------------------------
        // Contents
        // ------------------------------------------------------------------

        private static string Chest(CardSetTemplate set) { return "Set " + Zymmer.SetNumber(set) + " Chest Loot"; }
        private static string Wheel(CardSetTemplate set) { return "Set " + Zymmer.SetNumber(set) + " Wheels of Fate"; }

        public static List<CardTemplate> Cards(CardSetTemplate set)
        {
            List<CardTemplate> res = new List<CardTemplate>();
            foreach (CardTemplate t in Zymmer.Cards(Chest(set))) if (!res.Contains(t)) res.Add(t);
            foreach (CardTemplate t in Zymmer.Cards(Wheel(set))) if (!res.Contains(t)) res.Add(t);
            return res;
        }

        public static List<InventoryItemData> Equipment(CardSetTemplate set)
        {
            List<InventoryItemData> res = new List<InventoryItemData>();
            foreach (InventoryItemData i in Zymmer.Equipment(Chest(set))) if (!res.Contains(i)) res.Add(i);
            foreach (InventoryItemData i in Zymmer.Equipment(Wheel(set))) if (!res.Contains(i)) res.Add(i);
            return res;
        }

        public static List<InventoryMercenaryData> Mercs(CardSetTemplate set)
        {
            List<InventoryMercenaryData> res = Mercenaries.OfSet(set.m_Name);
            if (res.Count == 0)
            {
                foreach (InventoryMercenaryData m in Zymmer.MercenariesOf(Chest(set))) if (!res.Contains(m)) res.Add(m);
                foreach (InventoryMercenaryData m in Zymmer.MercenariesOf(Wheel(set))) if (!res.Contains(m)) res.Add(m);
            }
            return res;
        }

        public static List<InventoryDeckSleeve> SetSleeves(CardSetTemplate set)
        {
            string esc = Regex.Escape(set.m_Name.Trim());
            List<InventoryDeckSleeve> res = Sleeves.Matching("^Found in Chests for " + esc + @"\.$");
            foreach (InventoryDeckSleeve s in Sleeves.Matching("^Found in the Wheels of Fate for " + esc + @"\.$")) if (!res.Contains(s)) res.Add(s);
            return res;
        }

        private static int Count(CardSetTemplate set)
        {
            return Cards(set).Count + Equipment(set).Count + Mercs(set).Count + SetSleeves(set).Count;
        }

        /// <summary>
        /// A bought package into the purchase answer (the store's loot window shows it): copies of each card (never beyond
        /// 4 owned), the other items not owned yet.
        /// </summary>
        public static void Deliver(CardSetTemplate set, PurchaseItemResponse r, int copies)
        {
            int cards = 0, items = 0, mercs = 0;
            foreach (CardTemplate t in Cards(set))
            {
                int missing = Math.Min(copies, Collection.CopiesPerCard - Collection.Count(t.m_Id));
                if (missing <= 0) continue;
                r.GrantedCards.AddRange(Collection.GrantSilently(t.m_Id, missing));
                cards += missing;
            }
            List<InventoryItemData> rest = new List<InventoryItemData>(Equipment(set));
            rest.AddRange(SetSleeves(set).ConvertAll(s => (InventoryItemData)s));
            foreach (InventoryItemData i in rest)
            {
                if (Inventory.Count(i.m_Id) > 0) continue;
                inventory_bits b = Inventory.GrantSilently(i.m_Id);
                if (b != null) { r.GrantedInventory.Add(b); items++; }
            }
            foreach (InventoryMercenaryData m in Mercs(set)) if (Mercenaries.Grant(m, r)) mercs++;
            Debug.Log("[HexRevival] [Store] " + set.m_Name + " PvE full set: " + cards + " card(s), " + items + " item(s), " + mercs + " mercenar(ies)");
        }

        /// <summary>Dev: "pvefullset" = each set's package contents (cards, equipment, mercenaries, sleeves).</summary>
        public static string Describe()
        {
            List<string> o = new List<string>();
            foreach (InventoryItemData i in Items())
            {
                CardSetTemplate s = s_Sets[i.m_Id];
                o.Add(i.m_Name + ": " + Cards(s).Count + " PvE cards, " + Equipment(s).Count + " equipment, " + Mercs(s).Count + " mercenaries, " + SetSleeves(s).Count + " sleeves");
            }
            return string.Join("\n", o.ToArray());
        }
    }
}
