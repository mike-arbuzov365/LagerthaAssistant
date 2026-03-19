namespace LagerthaAssistant.Application.Constants;

public static class CallbackDataConstants
{
    public static class Nav
    {
        public const string Main = "nav:main";
    }

    public static class Lang
    {
        public const string Prefix = "lang:";
        public const string Ukrainian = "lang:uk";
        public const string English = "lang:en";
        public const string Spanish = "lang:es";
        public const string French = "lang:fr";
        public const string German = "lang:de";
        public const string Polish = "lang:pl";
        public const string Russian = "lang:ru";
        public const string GermanPolish = "lang:de_pl";
        public const string BackOnboarding = "lang:back_onboarding";
    }

    public static class Settings
    {
        public const string Prefix = "settings:";
        public const string Language = "settings:language";
        public const string SaveMode = "settings:savemode";
        public const string StorageMode = "settings:storagemode";
        public const string OneDrive = "settings:onedrive";
        public const string Notion = "settings:notion";
        public const string Back = "settings:back";
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
        public const string List = "vocab:list";
        public const string Url = "vocab:url";
        public const string Batch = "vocab:batch";
        public const string SaveYes = "vocab:save:yes";
        public const string SaveNo = "vocab:save:no";
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
    }
}
