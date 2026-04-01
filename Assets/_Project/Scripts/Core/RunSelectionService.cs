namespace EJR.Game.Core
{
    public static class RunSelectionService
    {
        private static string s_singleMapId = SharedRunCatalog.DefaultMapId;
        private static string s_singleDifficultyId = SharedRunCatalog.DefaultDifficultyId;

        public static string SingleMapId => SharedRunCatalog.GetMap(s_singleMapId).Id;
        public static string SingleDifficultyId => SharedRunCatalog.GetDifficulty(s_singleDifficultyId).Id;

        public static RunMapDefinition SingleMapDefinition => SharedRunCatalog.GetMap(SingleMapId);
        public static RunDifficultyDefinition SingleDifficultyDefinition => SharedRunCatalog.GetDifficulty(SingleDifficultyId);

        public static void SetSingleSelection(string mapId, string difficultyId)
        {
            s_singleMapId = SharedRunCatalog.GetMap(mapId).Id;
            s_singleDifficultyId = SharedRunCatalog.GetDifficulty(difficultyId).Id;
        }
    }
}
