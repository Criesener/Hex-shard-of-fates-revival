using System;
using System.Collections.Generic;
using Reckoning.Game;
using UnityEngine;

namespace HexRevival
{
    /// <summary>
    /// A Full Set's three bundle buttons as its variants (user 2026-11-02, with "Additional items in the store"): PvP,
    /// PvE and PvP + PvE in place of QTY 1, 3 and 10 (Store.Variants). The store's item context keeps a quantity of 1;
    /// its cost and name follow the variant chosen, so the BUY total, the money check and the confirmation read it
    /// (UIPremiumStoreItemContext.TotalCost, CheckIfValidPurchase, OnBuyClicked); the purchase delivers the variant
    /// (Store.PurchaseItem). The buttons' labels are rewritten every frame (the game's bindings set them back), and put
    /// back when another product is selected. The other products keep their quantities.
    /// </summary>
    public class StoreVariants : MonoBehaviour
    {
        private const string Widget = "Panel_R/Panel_Overlay/Frame_Buy_Widget/";
        private static readonly string[] Buttons = { "Button_Bundle_1", "Button_Bundle_3", "Button_Bundle_10" };

        private UIPremiumStoreViewModel m_Store;
        private float m_NextFind;
        private int m_Pending = -1;
        private readonly HashSet<GameObject> m_Listening = new HashSet<GameObject>();
        /// <summary>Labels rewritten, with their text before (put back when the variants are left).</summary>
        private readonly Dictionary<UILabel, string> m_Changed = new Dictionary<UILabel, string>();
        private readonly Dictionary<UILabel, Color> m_Colors = new Dictionary<UILabel, Color>();
        /// <summary>Item contexts renamed, with their name before.</summary>
        private readonly Dictionary<UIPremiumStoreItemContext, string> m_Renamed = new Dictionary<UIPremiumStoreItemContext, string>();
        private readonly Dictionary<UIPremiumStoreItemContext, int> m_Costs = new Dictionary<UIPremiumStoreItemContext, int>();

        private void LateUpdate()
        {
            if (m_Store == null && Time.realtimeSinceStartup >= m_NextFind)
            {
                m_NextFind = Time.realtimeSinceStartup + 1f;
                m_Store = FindObjectOfType<UIPremiumStoreViewModel>();
            }
            if (m_Store == null || !m_Store.isActiveAndEnabled) { m_Changed.Clear(); m_Colors.Clear(); m_Renamed.Clear(); m_Costs.Clear(); m_Listening.Clear(); return; }
            UIPremiumStoreItemContext ctx = null;
            try { ctx = m_Store.Context != null && m_Store.Context.Catalog != null ? m_Store.Context.Catalog.SelectedItem : null; } catch { }
            InventoryItemData item = ctx != null ? ctx.ItemData : null;
            int[] prices;
            InventoryItemData pve;
            if (item == null || !Store.Variants(item, out prices, out pve) || prices == null) { Restore(null); m_Pending = -1; return; }
            Restore(ctx);
            Listen();
            if (m_Pending >= 0) { Store.SetVariant(item, m_Pending); m_Pending = -1; }
            int v = Store.VariantOf(item);
            if (ctx.Quantity != 1) ctx.Quantity = 1;
            if (ctx.Cost != prices[v]) ctx.Cost = prices[v];
            if (!m_Renamed.ContainsKey(ctx)) { m_Renamed[ctx] = ctx.Name; m_Costs[ctx] = prices[0]; }
            string name = Store.VariantName(item, v);
            if (ctx.Name != name) ctx.Name = name;
            string[] names = { Loc.Get("variant_pvp"), Loc.Get("variant_pve"), Loc.Get("variant_both") };
            for (int k = 0; k < Buttons.Length; k++)
            {
                Transform b = m_Store.transform.Find(Widget + Buttons[k] + "/Labels_Bundle");
                if (b == null) continue;
                // "PvP + PvE" is too wide for the number's label: its "PvP +" takes the "Qty:" label's place.
                UILabel qty = Set(b.Find("Label_Quantity"), k == 2 ? names[0] + " +" : "");
                UILabel packs = Set(b.Find("Label_Packs"), k == 2 ? names[1] : names[k]);
                Set(b.Find("Label_Cost"), prices[k].ToString());
                // The chosen variant in gold, the others as the game draws them.
                foreach (UILabel l in new[] { qty, packs })
                {
                    if (l == null) continue;
                    if (!m_Colors.ContainsKey(l)) m_Colors[l] = l.color;
                    l.color = k == v ? new Color(0.98f, 0.76f, 0.2f, 1f) : m_Colors[l];
                }
            }
        }

        /// <summary>A click on a bundle button: its variant (applied next frame, after the game set its quantity).</summary>
        private void Listen()
        {
            for (int k = 0; k < Buttons.Length; k++)
            {
                Transform b = m_Store.transform.Find(Widget + Buttons[k]);
                if (b == null || m_Listening.Contains(b.gameObject)) continue;
                m_Listening.Add(b.gameObject);
                int variant = k;
                UIEventListener.Get(b.gameObject).onClick += go => { if (go != null) m_Pending = variant; };
            }
        }

        private UILabel Set(Transform t, string text)
        {
            UILabel l = t != null ? t.GetComponent<UILabel>() : null;
            if (l == null) return null;
            if (!m_Changed.ContainsKey(l)) m_Changed[l] = l.text;
            if (l.text != text) l.text = text;
            return l;
        }

        /// <summary>Puts back the labels (always but while a Full Set with variants is shown) and the other products' names.</summary>
        private void Restore(UIPremiumStoreItemContext keep)
        {
            if (keep == null)
            {
                foreach (KeyValuePair<UILabel, string> kv in m_Changed) if (kv.Key != null && kv.Key.name != "Label_Cost") kv.Key.text = kv.Value;
                foreach (KeyValuePair<UILabel, Color> kv in m_Colors) if (kv.Key != null) kv.Key.color = kv.Value;
               
                m_Changed.Clear();
                m_Colors.Clear();
            }
            // The other Full Sets show their own name again in the list (the price and labels follow the game's binding).
            List<UIPremiumStoreItemContext> back = new List<UIPremiumStoreItemContext>();
            foreach (KeyValuePair<UIPremiumStoreItemContext, string> kv in m_Renamed) if (kv.Key != keep) back.Add(kv.Key);
            foreach (UIPremiumStoreItemContext c in back)
            {
                try { c.Name = m_Renamed[c]; c.Cost = m_Costs[c]; } catch { }
                m_Renamed.Remove(c);
                m_Costs.Remove(c);
            }
        }
    }
}
