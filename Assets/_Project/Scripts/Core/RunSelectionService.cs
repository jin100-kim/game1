namespace EJR.Game.Core
{
    public static class RunSelectionService
    {
        private static string s_singleMapId = SharedRunCatalog.DefaultMapId;

        public static string SingleMapId => SharedRunCatalog.GetMap(s_singleMapId).Id;
        public static string SingleDifficultyId => SharedRunCatalog.DefaultDifficultyId;

        public static RunMapDefinition SingleMapDefinition => SharedRunCatalog.GetMap(SingleMapId);
        public static RunDifficultyDefinition SingleDifficultyDefinition => SharedRunCatalog.GetDifficulty(SingleDifficultyId);

        public static void SetSingleSelection(string mapId, string difficultyId = SharedRunCatalog.DefaultDifficultyId)
        {
            s_singleMapId = SharedRunCatalog.GetMap(mapId).Id;
        }
    }
}
