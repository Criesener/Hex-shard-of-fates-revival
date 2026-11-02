using System;
using System.Collections.Generic;
using Reckoning.Game;
using UnityEngine;

namespace HexRevival
{
    /// <summary>
    /// A Full Set's bundle buttons (user 2026-11-02).
    /// - With "Additional items in the store": its variants (Store.VariantCount) on two rows in place of QTY 1, 3 and
    ///   10: PvP, PvE, PvP+PvE, and the same x4 on a second row of copies of the three buttons, as far below as the
    ///   buttons are apart; the description panel under them is shortened by that row. The store's item context keeps a
    ///   quantity of 1; its cost and name follow the variant chosen, so the BUY total, the money check and the
    ///   confirmation read it (UIPremiumStoreItemContext.TotalCost, CheckIfValidPurchase, OnBuyClicked); the purchase
    ///   delivers the variant (Store.PurchaseItem).
    /// - Without: QTY 1, 2 and 4 (Store.FullSetQuantity): the QTY 3 button reads and buys 2, the QTY 10 one 4
    ///   (MapQuantity, on the store's SetQuantity).
    /// The buttons' labels are rewritten every frame (the game's bindings set them back), and put back when another
    /// product is selected. The other products keep their quantities.
    /// </summary>
    public class StoreVariants : MonoBehaviour
    {
        private const string Widget = "Panel_R/Panel_Overlay/Frame_Buy_Widget/";
        private static readonly string[] Buttons = { "Button_Bundle_1", "Button_Bundle_3", "Button_Bundle_10" };
        /// <summary>The quantities of the Full Sets' buttons without the variants.</summary>
        private static readonly int[] FullSetQuantities = { 1, 2, 4 };

        private UIPremiumStoreViewModel m_Store;
        private float m_NextFind;
        private int m_Pending = -1;
        /// <summary>The second row (copies of the three buttons), made once per store.</summary>
        private GameObject[] m_Row2;
        /// <summary>The description panel under the buttons: its height and place before it was shortened (null: as the game has it).</summary>
        private UIWidget m_Inset;
        private int m_InsetHeight;
        private Vector3 m_InsetPos;
        private bool m_InsetShort;
        private readonly HashSet<GameObject> m_Listening = new HashSet<GameObject>();
        /// <summary>Labels rewritten, with their text before (put back when the variants are left).</summary>
        private readonly Dictionary<UILabel, string> m_Changed = new Dictionary<UILabel, string>();
        private readonly Dictionary<UILabel, Color> m_Colors = new Dictionary<UILabel, Color>();
        /// <summary>Item contexts renamed, with their name before.</summary>
        private readonly Dictionary<UIPremiumStoreItemContext, string> m_Renamed = new Dictionary<UIPremiumStoreItemContext, string>();
        private readonly Dictionary<UIPremiumStoreItemContext, int> m_Costs = new Dictionary<UIPremiumStoreItemContext, int>();

        /// <summary>UIPremiumStoreViewModel.SetQuantity prefix: a Full Set's quantity, 1, 2 or 4 (or 1 with the variants).</summary>
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
                if (m_Store == null) { m_Row2 = null; m_Inset = null; m_InsetShort = false; }
                return;
            }
            UIPremiumStoreItemContext ctx = null;
            try { ctx = m_Store.Context != null && m_Store.Context.Catalog != null ? m_Store.Context.Catalog.SelectedItem : null; } catch { }
            InventoryItemData item = ctx != null ? ctx.ItemData : null;
            int[] prices;
            InventoryItemData pve;
            if (item != null && Store.Variants(item, out prices, out pve) && prices != null) { ShowVariants(ctx, item, prices); return; }
            m_Pending = -1;
            Row2(false);
            if (item != null && SpecialPacks.KindOf(item) == "fullset") { Restore(ctx); ShowQuantities(ctx); return; }
            Restore(null);
        }

        private void ShowVariants(UIPremiumStoreItemContext ctx, InventoryItemData item, int[] prices)
        {
            Restore(ctx);
            Row2(true);
            Listen();
            if (m_Pending >= 0) { Store.SetVariant(item, m_Pending); m_Pending = -1; }
            int v = Store.VariantOf(item);
            if (ctx.Quantity != 1) ctx.Quantity = 1;
            if (ctx.Cost != prices[v]) ctx.Cost = prices[v];
            if (!m_Renamed.ContainsKey(ctx)) { m_Renamed[ctx] = ctx.Name; m_Costs[ctx] = prices[0]; }
            string name = Store.VariantName(item, v);
            if (ctx.Name != name) ctx.Name = name;
            string both = Loc.Get("variant_both"), pve = Loc.Get("variant_pve");
            string head = both.EndsWith(pve) ? both.Substring(0, both.Length - pve.Length) : both;
            for (int k = 0; k < Store.VariantCount; k++)
            {
                Transform b = Button(k);
                b = b != null ? b.Find("Labels_Bundle") : null;
                if (b == null) continue;
                // The number's label shrinks what is wider than a number: "PvP+PvE" is split, its "PvP+" in the "Qty:"
                // label's place; on the x4 row the name takes that place and "x4" is the number.
                string kind = Loc.Get(Store.VariantKeys[k % 3]);
                UILabel qty = Set(b.Find("Label_Quantity"), k >= 3 ? kind + " " : k == 2 ? head : "");
                UILabel packs = Set(b.Find("Label_Packs"), k >= 3 ? "x4" : k == 2 ? pve : kind);
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

        /// <summary>A Full Set without the variants: QTY 1, 2 and 4 on the game's 1, 3 and 10 buttons.</summary>
        private void ShowQuantities(UIPremiumStoreItemContext ctx)
        {
            int q = Store.FullSetQuantity(ctx.ItemData, ctx.Quantity);
            if (ctx.Quantity != q) ctx.Quantity = q;
            for (int k = 1; k < Buttons.Length; k++)
            {
                Transform b = Button(k);
                b = b != null ? b.Find("Labels_Bundle") : null;
                if (b == null) continue;
                Set(b.Find("Label_Packs"), FullSetQuantities[k].ToString());
                Set(b.Find("Label_Cost"), (ctx.Cost * FullSetQuantities[k]).ToString());
            }
        }

        /// <summary>The bundle button k (3 to 5: the second row's).</summary>
        private Transform Button(int k)
        {
            if (k >= 3) return m_Row2 != null && m_Row2[k - 3] != null ? m_Row2[k - 3].transform : null;
            return m_Store.transform.Find(Widget + Buttons[k]);
        }

        /// <summary>The second row, made (copies of the three buttons, as far below them as they are apart) or hidden;
        /// the description panel under the buttons shortened by the row, or put back.</summary>
        private void Row2(bool show)
        {
            if (!show)
            {
                if (m_Row2 != null) foreach (GameObject g in m_Row2) if (g != null && g.activeSelf) g.SetActive(false);
                Inset(0);
                return;
            }
            float step = 0f;
            if (m_Row2 == null)
            {
                Transform a = m_Store.transform.Find(Widget + Buttons[0]), b = m_Store.transform.Find(Widget + Buttons[1]);
                UIWidget w = a != null ? a.GetComponent<UIWidget>() : null;
                if (w == null || b == null) return;
                float gap = Mathf.Abs(b.localPosition.x - a.localPosition.x) - w.width;
                step = w.height + Mathf.Max(gap, 0f);
                m_Row2 = new GameObject[Buttons.Length];
                for (int k = 0; k < Buttons.Length; k++)
                {
                    Transform src = m_Store.transform.Find(Widget + Buttons[k]);
                    if (src == null) continue;
                    GameObject g = Instantiate(src.gameObject, src.parent, false);
                    g.name = Buttons[k] + "_x4";
                    // Its own place: anchors off (they would put it back over the button copied), then a row lower.
                    foreach (UIRect r in g.GetComponentsInChildren<UIRect>(true))
                        if (AnchoredOutside(r, g.transform)) r.SetAnchor((Transform)null);
                    g.transform.localPosition = src.localPosition + new Vector3(0f, -step, 0f);
                    m_Row2[k] = g;
                }
                m_Step = step;
            }
            foreach (GameObject g in m_Row2) if (g != null && !g.activeSelf) g.SetActive(true);
            Inset(m_Step);
        }

        private float m_Step;

        /// <summary>The description panel under the buttons, its top lowered by by (0: as the game has it). Its bottom stays.</summary>
        private void Inset(float by)
        {
            if (m_Inset == null)
            {
                Transform t = m_Store.transform.Find(Widget + "Inset_Desc");
                m_Inset = t != null ? t.GetComponent<UIWidget>() : null;
                if (m_Inset == null) return;
            }
            if (by <= 0f)
            {
                if (!m_InsetShort) return;
                m_InsetShort = false;
                if (m_Inset.topAnchor.target != null) { m_Inset.topAnchor.absolute += m_InsetLowered; m_Inset.ResetAndUpdateAnchors(); }
                else { m_Inset.height = m_InsetHeight; m_Inset.transform.localPosition = m_InsetPos; }
                KeepButtons(-m_InsetLowered);
                return;
            }
            if (m_InsetShort) return;
            m_InsetShort = true;
            int d = Mathf.RoundToInt(by);
            m_InsetLowered = d;
            if (m_Inset.topAnchor.target != null) { m_Inset.topAnchor.absolute -= d; m_Inset.ResetAndUpdateAnchors(); }
            else
            {
                m_InsetHeight = m_Inset.height;
                m_InsetPos = m_Inset.transform.localPosition;
                // The height shrinks around the pivot: moved down by the part of it above the pivot, so the bottom stays.
                m_Inset.height = m_InsetHeight - d;
                m_Inset.transform.localPosition = m_InsetPos + new Vector3(0f, -d * m_Inset.pivotOffset.y, 0f);
            }
            KeepButtons(d);
        }

        /// <summary>
        /// The game's buttons are anchored to the panel's top: their anchors on it raised by by (lowered back with -by), so
        /// they stay where they were when the panel's top moves.
        /// </summary>
        private void KeepButtons(int by)
        {
            foreach (string name in Buttons)
            {
                Transform b = m_Store.transform.Find(Widget + name);
                if (b == null) continue;
                foreach (UIRect r in b.GetComponentsInChildren<UIRect>(true))
                {
                    bool moved = false;
                    foreach (UIRect.AnchorPoint a in new[] { r.bottomAnchor, r.topAnchor })
                        if (a != null && a.target == m_Inset.transform && a.relative > 0.5f) { a.absolute += by; moved = true; }
                    if (moved) r.ResetAndUpdateAnchors();
                }
            }
        }

        private int m_InsetLowered;

        private static bool AnchoredOutside(UIRect r, Transform root)
        {
            foreach (UIRect.AnchorPoint a in new[] { r.leftAnchor, r.rightAnchor, r.bottomAnchor, r.topAnchor })
                if (a != null && a.target != null && !a.target.IsChildOf(root)) return true;
            return false;
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
