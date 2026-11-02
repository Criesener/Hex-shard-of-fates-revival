using System;
using System.Collections.Generic;
using Reckoning.Game;
using UnityEngine;

namespace HexRevival
{
    /// <summary>
    /// A Full Set's bundle buttons (user 2026-11-02).
    /// - With "Additional items in the store": its variants (Store.VariantCount) in place of QTY 1, 3 and 10: PvP, PvE,
    ///   PvP+PvE, and PvP+PvE x4 on a 4th button, a copy of the QTY 10 one placed under the row. The store's item context
    ///   keeps a quantity of 1; its cost and name follow the variant chosen, so the BUY total, the money check and the
    ///   confirmation read it (UIPremiumStoreItemContext.TotalCost, CheckIfValidPurchase, OnBuyClicked); the purchase
    ///   delivers the variant (Store.PurchaseItem).
    /// - Without: QTY 1 and 4 only (Store.FullSetQuantity): the QTY 3 button reads 4 and buys 4 (MapQuantity, on the
    ///   store's SetQuantity), the QTY 10 one is hidden.
    /// The buttons' labels are rewritten every frame (the game's bindings set them back), and put back when another
    /// product is selected. The other products keep their quantities.
    /// </summary>
    public class StoreVariants : MonoBehaviour
    {
        private const string Widget = "Panel_R/Panel_Overlay/Frame_Buy_Widget/";
        private static readonly string[] Buttons = { "Button_Bundle_1", "Button_Bundle_3", "Button_Bundle_10" };
        private const string FourthName = "Button_Bundle_Variant4";

        private UIPremiumStoreViewModel m_Store;
        private float m_NextFind;
        private int m_Pending = -1;
        /// <summary>The 4th button (a copy of the QTY 10 one), made once per store.</summary>
        private GameObject m_Fourth;
        /// <summary>The QTY 10 button, hidden while a Full Set sells by 1 or 4.</summary>
        private GameObject m_Hidden;
        private readonly HashSet<GameObject> m_Listening = new HashSet<GameObject>();
        /// <summary>Labels rewritten, with their text before (put back when the variants are left).</summary>
        private readonly Dictionary<UILabel, string> m_Changed = new Dictionary<UILabel, string>();
        private readonly Dictionary<UILabel, Color> m_Colors = new Dictionary<UILabel, Color>();
        /// <summary>Item contexts renamed, with their name before.</summary>
        private readonly Dictionary<UIPremiumStoreItemContext, string> m_Renamed = new Dictionary<UIPremiumStoreItemContext, string>();
        private readonly Dictionary<UIPremiumStoreItemContext, int> m_Costs = new Dictionary<UIPremiumStoreItemContext, int>();

        /// <summary>UIPremiumStoreViewModel.SetQuantity prefix: a Full Set's quantity, 1 or 4 (or 1 with the variants).</summary>
        public static int MapQuantity(UIPremiumStoreViewModel store, int quantity)
        {
            try
            {
                UIPremiumStoreItemContext ctx = store.Context.Catalog.SelectedItem;
                return ctx != null ? Store.FullSetQuantity(ctx.ItemData, quantity) : quantity;
            }
            catch { return quantity; }
        }

        private void LateUpdate()
        {
            if (m_Store == null && Time.realtimeSinceStartup >= m_NextFind)
            {
                m_NextFind = Time.realtimeSinceStartup + 1f;
                m_Store = FindObjectOfType<UIPremiumStoreViewModel>();
            }
            if (m_Store == null || !m_Store.isActiveAndEnabled)
            {
                m_Changed.Clear(); m_Colors.Clear(); m_Renamed.Clear(); m_Costs.Clear(); m_Listening.Clear();
                if (m_Store == null) { m_Fourth = null; m_Hidden = null; }
                return;
            }
            UIPremiumStoreItemContext ctx = null;
            try { ctx = m_Store.Context != null && m_Store.Context.Catalog != null ? m_Store.Context.Catalog.SelectedItem : null; } catch { }
            InventoryItemData item = ctx != null ? ctx.ItemData : null;
            int[] prices;
            InventoryItemData pve;
            if (item != null && Store.Variants(item, out prices, out pve) && prices != null) { Show(false); ShowVariants(ctx, item, prices); return; }
            m_Pending = -1;
            Fourth(false);
            if (item != null && SpecialPacks.KindOf(item) == "fullset") { Restore(ctx); Show(true); ShowOneOrFour(ctx); return; }
            Show(false);
            Restore(null);
        }

        private void ShowVariants(UIPremiumStoreItemContext ctx, InventoryItemData item, int[] prices)
        {
            Restore(ctx);
            Fourth(true);
            Listen();
            if (m_Pending >= 0) { Store.SetVariant(item, m_Pending); m_Pending = -1; }
            int v = Store.VariantOf(item);
            if (ctx.Quantity != 1) ctx.Quantity = 1;
            if (ctx.Cost != prices[v]) ctx.Cost = prices[v];
            if (!m_Renamed.ContainsKey(ctx)) { m_Renamed[ctx] = ctx.Name; m_Costs[ctx] = prices[0]; }
            string name = Store.VariantName(item, v);
            if (ctx.Name != name) ctx.Name = name;
            for (int k = 0; k < Store.VariantCount; k++)
            {
                Transform b = Button(k);
                b = b != null ? b.Find("Labels_Bundle") : null;
                if (b == null) continue;
                // The number's label shrinks what is wider than a number: "PvP+PvE" is split, its "PvP+" in the "Qty:"
                // label's place (x4: "PvP+PvE" there, "x4" as the number).
                string both = Loc.Get("variant_both"), pve = Loc.Get("variant_pve");
                string head = both.EndsWith(pve) ? both.Substring(0, both.Length - pve.Length) : both;
                UILabel qty = Set(b.Find("Label_Quantity"), k == 2 ? head : k == 3 ? both + " " : "");
                UILabel packs = Set(b.Find("Label_Packs"), k == 2 ? pve : k == 3 ? "x4" : Loc.Get(Store.VariantKeys[k]));
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

        /// <summary>A Full Set without the variants: QTY 1 and 4, the 3 button standing for 4.</summary>
        private void ShowOneOrFour(UIPremiumStoreItemContext ctx)
        {
            int q = Store.FullSetQuantity(ctx.ItemData, ctx.Quantity);
            if (ctx.Quantity != q) ctx.Quantity = q;
            Transform b = Button(1);
            b = b != null ? b.Find("Labels_Bundle") : null;
            if (b == null) return;
            Set(b.Find("Label_Packs"), "4");
            Set(b.Find("Label_Cost"), (ctx.Cost * 4).ToString());
        }

        /// <summary>The bundle button k (3: the 4th one).</summary>
        private Transform Button(int k)
        {
            if (k == 3) return m_Fourth != null ? m_Fourth.transform : null;
            return m_Store.transform.Find(Widget + Buttons[k]);
        }

        /// <summary>The 4th button, made (a copy of the QTY 10 one, placed under the QTY 3 one) or hidden.</summary>
        private void Fourth(bool show)
        {
            if (!show) { if (m_Fourth != null && m_Fourth.activeSelf) m_Fourth.SetActive(false); return; }
            if (m_Fourth == null)
            {
                Transform ten = m_Store.transform.Find(Widget + Buttons[2]);
                Transform three = m_Store.transform.Find(Widget + Buttons[1]);
                if (ten == null || three == null) return;
                m_Fourth = Instantiate(ten.gameObject, ten.parent, false);
                m_Fourth.name = FourthName;
                // Its own place: anchors off (they would put it back over the QTY 10 button), then under the row.
                foreach (UIRect r in m_Fourth.GetComponentsInChildren<UIRect>(true))
                    if (AnchoredOutside(r, m_Fourth.transform)) r.SetAnchor((Transform)null);
                Bounds bb = NGUIMath.CalculateRelativeWidgetBounds(three);
                m_Fourth.transform.localPosition = three.localPosition + new Vector3(0f, -(bb.size.y + 12f), 0f);
            }
            if (!m_Fourth.activeSelf) m_Fourth.SetActive(true);
        }

        private static bool AnchoredOutside(UIRect r, Transform root)
        {
            foreach (UIRect.AnchorPoint a in new[] { r.leftAnchor, r.rightAnchor, r.bottomAnchor, r.topAnchor })
                if (a != null && a.target != null && !a.target.IsChildOf(root)) return true;
            return false;
        }

        /// <summary>Hides the QTY 10 button (a Full Set sold by 1 or 4), or shows it again.</summary>
        private void Show(bool hideTen)
        {
            Transform ten = m_Store.transform.Find(Widget + Buttons[2]);
            if (ten == null) return;
            if (hideTen && ten.gameObject.activeSelf) { m_Hidden = ten.gameObject; ten.gameObject.SetActive(false); }
            else if (!hideTen && m_Hidden != null) { m_Hidden.SetActive(true); m_Hidden = null; }
        }

        /// <summary>A click on a bundle button: its variant (applied next frame, after the game set its quantity).</summary>
        private void Listen()
        {
            for (int k = 0; k < Store.VariantCount; k++)
            {
                Transform b = Button(k);
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

        /// <summary>Puts back the labels (when keep is null) and the other products' names.</summary>
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
