using Mongle.Core;
using Mongle.Toy;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>Polaroid-style photo card: shows an image (or a gentle placeholder) with its metadata caption.</summary>
    public class PhotoPanel
    {
        public GameObject Root { get; private set; }
        RawImage image; Text caption, placeholder;
        public Button Primary, Secondary;

        public PhotoPanel(Transform parent, string primary, string secondary)
        {
            var p = UiKit.Panel(parent, "PhotoPanel", new Color(1f, 0.99f, 0.95f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 970), true);
            Root = p.gameObject;
            var frame = UiKit.Panel(p.transform, "Frame", new Color(0.93f, 0.9f, 0.85f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -395), new Vector2(680, 680), false);
            var ri = UiKit.Fixed(frame.transform, "Image", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 640));
            image = ri.gameObject.AddComponent<RawImage>(); image.raycastTarget = false; image.color = Color.white;
            placeholder = UiKit.Label(frame.transform, "", 34, UiKit.Brown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 200));
            caption = UiKit.Label(p.transform, "", 34, UiKit.Brown, new Vector2(0.5f, 0f), new Vector2(0, 195), new Vector2(700, 70), TextAnchor.MiddleCenter, FontStyle.Normal);
            Primary = UiKit.MakeButton(p.transform, primary, UiKit.Peach, new Vector2(0.5f, 0f), new Vector2(secondary == null ? 0 : 190, 85), new Vector2(340, 100), null, 36);
            if (secondary != null) Secondary = UiKit.MakeButton(p.transform, secondary, UiKit.Mint, new Vector2(0.5f, 0f), new Vector2(-190, 85), new Vector2(340, 100), null, 36);
            Root.SetActive(false);
        }

        public void Show(Texture tex, string captionText, string missingText = "사진 파일을 찾을 수 없어요")
        {
            Root.SetActive(true);
            image.texture = tex;
            image.enabled = tex != null;
            placeholder.text = tex == null ? missingText : "";
            caption.text = captionText;
        }
        public void Hide() { Root.SetActive(false); }
        public bool ShowingPlaceholder => placeholder != null && !string.IsNullOrEmpty(placeholder.text);
    }
}
