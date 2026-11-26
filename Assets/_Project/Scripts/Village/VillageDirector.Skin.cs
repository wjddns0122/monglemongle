#if UNITY_EDITOR
using System.Linq;
using Mongle.Data;
using Mongle.UI;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// D-035: the village screen built with sample contents for its editable prefab (Mongle > UI menu, UiSkinBaker): the HUD as it
    /// shows, every panel the village opens (friend's bubble, star hunt, ride bar,
    /// dress list, house bar) built once with sample lines and switched off.
    /// </summary>
    public partial class VillageDirector
    {
        public GameObject BuildSkinSample(ContentCatalog catalog)
        {
            Catalog = catalog;
            BuildUi();
            bubbleText.text = "안녕! 와줬구나.";
            // (the shop, the 보관함 and the 퀘스트 window are 11-26 views: Shop / StorageBox / QuestWindow.prefab in Assets/_UI수정/3_마을)
            // the friends' quest marks and the star hunt pill
            BuildQuestUi();
            huntText.text = "★ 별 찾기 1/3   2:30";
            // the house bar (the dress-up is the 11-26 friend card, the claw machine the 11-26 Claw.prefab)
            BuildHouseUi();
            houseText.text = "토끼가 이사 왔어요! 집을 놓을 곳을 골라 주세요";
            return canvas.gameObject;
        }
    }
}
#endif
