#if UNITY_EDITOR
using System.Linq;
using Mongle.Data;
using Mongle.UI;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// D-035: the village screen built with sample contents for its editable prefab (Mongle > UI menu, UiSkinBaker): the HUD as it
    /// shows, every panel the village opens (shop, 보관함, quest window, friend's bubble, star hunt, claw machine, ride bar,
    /// dress list, house bar, album) built once with sample lines and switched off.
    /// </summary>
    public partial class VillageDirector
    {
        public GameObject BuildSkinSample(ContentCatalog catalog)
        {
            Catalog = catalog;
            BuildUi();
            status.text = "몽이의 마을 · 3월 2일 입주";
            coinText.text = "120";
            storageBadge.SetActive(true); storageBadgeText.text = "새 2";
            questBadge.SetActive(true); questBadgeText.text = "새 1";
            questPanel.gameObject.SetActive(true); questText.text = "별 세 개 찾아 주기 1/3\n꽃밭 물 주기 0/2";
            bubbleText.text = "안녕! 와줬구나.";
            album.Show(null, "2026-03-02 · 몽이 · 활짝 웃기"); album.Hide();
            // the shop: a sample of the things for sale (and, kept off, one of the shop's requests)
            var world = new WorldState(); world.Earn(300); world.SetQuestDefinitions(catalog.quests);
            var shop = catalog.shops.FirstOrDefault(s => s != null && s.items != null && s.items.Any(i => i != null));
            if (shop != null)
            {
                var asks = world.QuestsOf(shop.id).ToList();
                GameObject askRow = null;
                if (asks.Count > 0)
                {
                    Shop.ShowShop(shop, world, "어서 와요! 천천히 둘러봐요", asks, true);
                    var cards = Shop.CardsRoot;
                    if (cards.childCount > 0) { askRow = cards.GetChild(0).gameObject; askRow.transform.SetParent(null, false); }
                }
                Shop.ShowShop(shop, world, "어서 와요! 천천히 둘러봐요", asks);
                if (askRow != null) { askRow.transform.SetParent(Shop.CardsRoot, false); askRow.SetActive(false); }
                Shop.Hide();
            }
            foreach (var f in catalog.furniture.Where(f => f != null).Take(3)) world.AddToInventory(f.id);
            Storage.ShowStorage(world, catalog.furniture); Storage.Hide();
            // the friends' quests
            BuildQuestUi();
            QuestWindow.Render(0, new[]
            {
                new QuestBoard.Row { key = "sample", icon = "토", iconColor = UiKit.Pink, title = "별 세 개 찾아 주기", detail = "토끼의 부탁 · 공원 어딘가에 별이 숨어 있어요", progress = "1/3", reward = "+22", action = "같이 찾기", actionColor = UiKit.Pink, onAction = () => { } },
                new QuestBoard.Row { key = "done", icon = "곰", iconColor = UiKit.Sky, title = "꽃밭 물 주기", detail = "곰의 부탁", progress = "2/2", done = true },
            }, "", new[] { "진행 중 1", "완료 1", "내 이야기" });
            QuestWindow.Hide();
            Dialog.Show("토끼", UiKit.Pink, "안녕! 부탁 하나 들어줄래?", "· 별 세 개 찾기",
                new[] { new FriendDialog.Choice("좋아, 할게!", UiKit.Peach, () => { }) }, 2,
                new[] { new FriendDialog.Choice("나중에", UiKit.Mint, () => { }) });
            Dialog.Hide();
            huntText.text = "★ 별 찾기 1/3   2:30";
            // the claw machine, the ride bar, the dress list and the house bar
            BuildClawUi();
            clawTimer.text = "다음 하트 12:30"; clawTip.text = "방향키로 집게를 캡슐 위로 움직이고 '내리기!' (Space)"; clawResult.text = "캡슐이 비어 있었어요!";
            BuildRideUi();
            rideCaption.text = "몽이 · 회전목마 타는 중";
            BuildDressPanel();
            var accs = catalog.AllAccessories.Take(3).ToList();
            dressTitle.text = "몽이 꾸며주기 · 누르면 입히고, 다시 누르면 벗어요";
            LayDress(accs, a => accs.Count > 0 && a == accs[0], _ => 1);
            dressPanel.SetActive(false);
            BuildHouseUi();
            houseText.text = "토끼가 이사 왔어요! 집을 놓을 곳을 골라 주세요";
            return canvas.gameObject;
        }
    }
}
#endif
