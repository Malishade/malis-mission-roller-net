using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;

public class RollerTerminal
{
    public static void Use(byte difficultyValue)
    {
        var closestTerminal = DynelManager.AllDynels
            .Where(x => x.Identity.Type == IdentityType.MissionTerminal)
            .OrderBy(x => x.DistanceFrom(DynelManager.LocalPlayer))
            .FirstOrDefault();

        if (closestTerminal == null)
        {
            Chat.WriteLine("No terminal found");
            return;
        }

        if (closestTerminal.DistanceFrom(DynelManager.LocalPlayer) > 7f)
        {
            Chat.WriteLine("Too far away. Move closer to the terminal");
            return;
        }

        new MissionTerminal(closestTerminal).RequestMissions(difficulty: difficultyValue);
    }
}