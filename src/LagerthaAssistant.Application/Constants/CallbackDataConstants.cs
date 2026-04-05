namespace LagerthaAssistant.Application.Constants;

public static class CallbackDataConstants
{
    public static class Nav
    {
        public const string Main = "nav:main";
        public const string Weekly = "nav:weekly";
        public const string Vocab = "nav:vocab";
    }

    public static class Lang
    {
        public const string Prefix = "lang:";
        public const string Ukrainian = "lang:uk";
        public const string English = "lang:en";
    }

    public static class Settings
    {
        public const string Prefix = "settings:";
        public const string Language = "settings:language";
        public const string SaveMode = "settings:savemode";
        public const string StorageMode = "settings:storagemode";
        public const string Ai = "settings:ai";
        public const string OneDrive = "settings:onedrive";
        public const string Notion = "settings:notion";
        public const string Legacy = "settings:legacy";
        public const string Back = "settings:back";
    }

    public static class Ai
    {
        public const string Prefix = "ai:";
        public const string Provider = "ai:provider";
        public const string Model = "ai:model";
        public const string KeySet = "ai:key:set";
        public const string KeyRemove = "ai:key:remove";
        public const string Back = "ai:back";

        public const string ProviderSetPrefix = "ai:provider:set:";
        public const string ModelSetPrefix = "ai:model:set:";
    }

    public static class SaveMode
    {
        public const string Prefix = "savemode:";
        public const string Auto = "savemode:auto";
        public const string Ask = "savemode:ask";
        public const string Off = "savemode:off";
    }

    public static class OneDrive
    {
        public const string Prefix = "onedrive:";
        public const string Login = "onedrive:login";
        public const string Logout = "onedrive:logout";
        public const string CheckLogin = "onedrive:check_login";
        public const string SyncNow = "onedrive:sync_now";
        public const string RebuildIndex = "onedrive:index_rebuild";
        public const string RebuildIndexConfirm = "onedrive:index_rebuild_confirm";
        public const string ClearCache = "onedrive:cache_clear";
        public const string ClearCacheConfirm = "onedrive:cache_clear_confirm";
    }

    public static class StorageMode
    {
        public const string Prefix = "storagemode:";
        public const string Local = "storagemode:local";
        public const string Graph = "storagemode:graph";
    }

    public static class Vocab
    {
        public const string Prefix = "vocab:";
        public const string Add = "vocab:add";
        public const string Stats = "vocab:stats";
        public const string ListLegacy = "vocab:list";
        public const string Url = "vocab:url";
        public const string ImportSourcePhoto = "vocab:url:source:photo";
        public const string ImportSourceFile = "vocab:url:source:file";
        public const string ImportSourceUrl = "vocab:url:source:url";
        public const string ImportSourceText = "vocab:url:source:text";
        public const string UrlSelectAll = "vocab:url:select_all";
        public const string UrlCancel = "vocab:url:cancel";
        public const string Batch = "vocab:batch";
        public const string SaveYes = "vocab:save:yes";
        public const string SaveNo = "vocab:save:no";
        public const string SaveBatchYes = "vocab:save_batch:yes";
        public const string SaveBatchNo = "vocab:save_batch:no";
    }

    public static class Food
    {
        public const string Prefix = "food:";
        public const string Menu = "food:menu";
        public const string Inventory = "food:inventory";
        public const string Shopping = "food:shopping";
    }

    public static class Inventory
    {
        public const string Prefix = "inventory:";
        public const string List = "inventory:list";
        public const string ListAvailable = "inventory:list:available";
        public const string ListMissing = "inventory:list:missing";
        public const string Search = "inventory:search";
        public const string Add = "inventory:add";
        public const string Stats = "inventory:stats";
        public const string Adjust = "inventory:adjust";
        public const string Min = "inventory:min";
        public const string ResetStock = "inventory:reset_stock";
        public const string ResetStockConfirm = "inventory:reset_stock:confirm";
        public const string Suggest = "inventory:suggest";
        public const string PhotoRestock = "inventory:photo:restock";
        public const string PhotoConsume = "inventory:photo:consume";
        public const string PhotoApplyAll = "inventory:photo:apply_all";
        public const string PhotoSelect = "inventory:photo:select";
        public const string PhotoCancel = "inventory:photo:cancel";
        public const string PhotoStoreAdd = "inventory:photo:store:add";
        public const string PhotoStoreSkip = "inventory:photo:store:skip";
        public const string PhotoStoreSelectPrefix = "inventory:photo:store:sel:";
        public const string PhotoStorePickExisting = "inventory:photo:store:pick";
        public const string PhotoUnknownAddAll = "inventory:photo:unknown:add_all";
        public const string PhotoUnknownSelect = "inventory:photo:unknown:select";
        public const string PhotoUnknownLink = "inventory:photo:unknown:link";
        public const string PhotoUnknownSkip = "inventory:photo:unknown:skip";
        public const string CartPrefix = "inventory:cart:";
        public const string Manage = "inventory:manage";
        public const string Move = "inventory:move";
    }

    public static class Shop
    {
        public const string Prefix = "shop:";
        public const string Add = "shop:add";
        public const string List = "shop:list";
        public const string Delete = "shop:delete";
    }

    public static class Weekly
    {
        public const string Prefix = "weekly:";
        public const string View = "weekly:view";
        public const string Plan = "weekly:plan";
        public const string Calories = "weekly:calories";
        public const string Favourites = "weekly:favourites";
        public const string Log = "weekly:log";
        public const string Create = "weekly:create";
        public const string CreateConfirm = "weekly:create:yes";
        public const string CreateCancel = "weekly:create:no";
        public const string PhotoConfirm = "weekly:photo:yes";
        public const string PhotoCancel = "weekly:photo:no";
        public const string DailyGoal = "weekly:goal";
        public const string Diversity = "weekly:diversity";
        public const string Analytics = "weekly:analytics";
    }
}
