using Mongle.Views;
using TMPro;
using UnityEngine;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// One request row as the 11-26 퀘스트 window draws it (Figma 88:2130: 88:2206 + 88:2208 plate, 88:2218 icon, 88:2219 title,
    /// 88:2221 line, 88:2225 coin pill, 88:2222 button, done row 88:2256 with ♥ 88:2263), at any scale <c>k</c> and any width:
    /// the plate is a <see cref="Mongle.UI2.SoftRect"/> with the SVG's values (r 28.65, drop shadow 1,1 σ1 50 %; stitch white
    /// 3 px, 10.06 / 10.06 round dashes with their own shadow), the icon a drawn circle, the left side runs from the left and
    /// the coin pill and the button keep to the right. Used by the shop (k 0.7) and the 퀘스트 window (k 1).
    /// </summary>
    public static class AskRowBuild
    {
        public const float W = 1356.65f, H = 173.73f;
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.5f);

        /// <summary>The look every row shares (Figma's colours; the circle colours of 88:2218 / 88:2231 / 88:2243 / 88:2260).</summary>
        public static AskStyle Style() => new AskStyle
        {
            rowColor = Color.white, rowDoneColor = Hex("#F6F0E7"), doneIconColor = Hex("#ECE3D6"),
            giverColors = new[] { Hex("#FF8FAB"), Hex("#FFE1E6"), Hex("#FFF492") },
            openButton = Art("Quest/btn_sky.png"), goButton = Art("Quest/btn_yellow.png"),
        };

        /// <summary>A row at frame place (x, y) inside a parent of size <paramref name="parentSize"/>, <paramref name="w"/> wide, sizes times <paramref name="k"/>.</summary>
        public static AskRow Make(Transform parent, string name, float x, float y, float w, float k, Vector2 parentSize)
        {
            float h = H * k; var rs = new Vector2(w, h);
            var row = Node(parent, name, x, y, w, h, TL, parentSize);
            var plateNode = Node(row, "Plate", 0f, 0f, w, h, TL, rs);
            plateNode.anchorMin = Vector2.zero; plateNode.anchorMax = Vector2.one; plateNode.offsetMin = plateNode.offsetMax = Vector2.zero;
            var plate = Shape(plateNode.gameObject, Color.white, Vector4.one * 28.65f * k);
            plate.shadowColor = Shadow; plate.shadowOffset = new Vector2(1f, -1f); plate.shadowBlur = 1f;
            plate.stitch = true; plate.stitchColor = Color.white; plate.stitchWidth = 3f * k; plate.dash = 10.06f * k; plate.gap = 10.06f * k; plate.roundCaps = true;
            plate.stitchPad = new Vector4(15.67f, 13.83f, 15.68f, 13.84f) * k; plate.stitchRadius = 28.65f * k; plate.stitchShadow = Shadow;
            // the round icon: the giver's letter, or a picture of the thing on it (a little bigger, like a sticker)
            float d = 119.98f * k; var ib = new Vector2(d, d);
            var iconNode = Node(row, "Icon", 31.5f * k, 26.97f * k, d, d, TL, rs);
            var icon = Shape(iconNode.gameObject, Hex("#FF8FAB"), Vector4.one * d * 0.5f);
            var initial = Words(iconNode, "Initial", "다", 48f * k, Brown, 0f, -2f * k, d, d, TL, HorizontalAlignmentOptions.Center, null, ib);
            var picture = Picture(iconNode, "Picture", "Quest/btn_sky.png", -6f * k / 0.7f, -6f * k / 0.7f, d + 12f * k / 0.7f, d + 12f * k / 0.7f, TL, ib);
            picture.sprite = null; picture.enabled = false;
            // right side: the button 46 px (Figma) from the edge, the coin pill 83 px left of it
            var btnBox = new Vector2(296.5f * k, 94.5f * k);
            float btnX = w - 46.07f * k - btnBox.x, rewardX = btnX - 83f * k - 144f * k;
            var button = PictureButton(row, "Btn", "Quest/btn_sky.png", btnX, 38.78f * k, btnBox.x, btnBox.y, TR, rs);
            var label = Words(button.transform, "Label", "수락할게요", 40f * k, Brown, 0f, -2.6f * k, btnBox.x, btnBox.y, TL, HorizontalAlignmentOptions.Center, null, btnBox);
            label.enableAutoSizing = true; label.fontSizeMin = 18f; label.fontSizeMax = 40f * k;
            var pill = Picture(row, "Reward", "Quest/reward.png", rewardX, 53.78f * k, 144f * k, 66.5f * k, TR, rs);
            var reward = Words(pill.transform, "Amount", "+10", 30f * k, Brown, (856.46f - 787.08f) * k, (65.88f - 53.78f) * k, 80f * k, 39f * k, TL, HorizontalAlignmentOptions.Left, null, new Vector2(144f * k, 66.5f * k));
            // left side: the title and the line run up to the coin pill
            float textW = rewardX - 12f - 178.8f * k;
            var title = Words(row, "Title", "첫 화분 놓기", 48f * k, Brown, 177.13f * k, 23f * k, textW, 62f * k, TL, HorizontalAlignmentOptions.Left, null, rs);
            title.enableAutoSizing = true; title.fontSizeMin = 22f; title.fontSizeMax = 48f * k;
            var line = Words(row, "Line", "다람 : 화분을 하나 사서 마을에 놓아 줄래요?", 30f * k, Brown, 178.8f * k, 93.35f * k, textW, 39f * k, TL, HorizontalAlignmentOptions.Left, null, rs);
            line.enableAutoSizing = true; line.fontSizeMin = 15f; line.fontSizeMax = 30f * k;
            var done = Node(row, "Done", 0f, 0f, w, h, TL, rs);
            done.anchorMin = Vector2.zero; done.anchorMax = Vector2.one; done.offsetMin = done.offsetMax = Vector2.zero;
            Picture(done, "Heart", "Quest/heart.png", w - 285.07f * k, 69.19f * k, 46f * k, 41.5f * k, TR, rs);
            Words(done, "Words", "완료", 40f * k, Brown, w - 216.46f * k, 60.25f * k, 80f * k / 0.7f, 52f * k, TR, HorizontalAlignmentOptions.Left, "quest.done", rs);
            done.gameObject.SetActive(false);
            return new AskRow { root = row.gameObject, plate = plate, icon = icon, picture = picture, initial = initial, title = title, line = line, reward = reward, rewardPill = pill.gameObject, done = done.gameObject, button = button, buttonText = label };
        }
    }
}
