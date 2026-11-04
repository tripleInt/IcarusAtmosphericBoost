using System;

namespace IcarusAtmosphericBoost
{
    internal static class DriveLevels
    {
        internal static int Read(PlayerController controller)
        {
            var history = controller.gameData.history;
            var techs = LDB.techs.dataArray;
            string family = null;
            // Discover the family from the original low-altitude flight unlock.
            // thrusterLevel is a capability flag (warp is 3), not the tech level.
            foreach (TechProto tech in techs)
            {
                if (tech.UnlockFunctions == null || tech.UnlockValues == null) continue;
                for (int i = 0; i < Math.Min(tech.UnlockFunctions.Length, tech.UnlockValues.Length); i++)
                    if (tech.UnlockFunctions[i] == 4 && tech.UnlockValues[i] == 1)
                        family = tech.Name;
                if (family != null) break;
            }
            int level = 0;
            if (family != null && history?.techStates != null)
                foreach (TechProto tech in techs)
                    if (tech.Name == family && history.techStates.TryGetValue(tech.ID, out TechState state))
                    {
                        int completed = state.unlocked ? state.curLevel : state.curLevel - 1;
                        if (completed >= tech.Level) level = Math.Max(level, completed);
                    }
            // Sandbox can grant movement abilities without researching the tree.
            int abilityLevel = controller.mecha.thrusterLevel;
            return Math.Max(level, abilityLevel >= 3 ? 4 : Math.Max(0, abilityLevel));
        }
    }
}
