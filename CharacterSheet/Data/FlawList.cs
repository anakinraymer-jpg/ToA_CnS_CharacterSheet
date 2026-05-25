namespace CharacterSheet.Data;

/// <summary>
/// All Crown &amp; Skull flaws, alphabetically sorted.
/// Shares the <see cref="SkillEntry"/> record type so they work in a
/// SkillPickerBox via the EntrySource dependency property.
/// </summary>
public static class FlawList
{
    public static readonly IReadOnlyList<SkillEntry> All = new List<SkillEntry>
    {
        new("Addict",
            "You have a specific vice you cannot deny. If you can't get your fix, the GM will ask for a roll vs. attrition in some form."),

        new("Ancient",
            "You are old! Muscle, Jump, Climber, Breakfall, and Stealth skills may not exceed 9, but you earn respect for your advanced age."),

        new("Ascetic",
            "You frown on possessions. Never exceed 5 equipment."),

        new("Bad Reputation",
            "You've done things… terrible things, and people know about it. You've done harm, broken oaths, or let someone down."),

        new("Crazy",
            "When faced with a difficult choice, choose randomly."),

        new("Disorganized",
            "Where'd you put that? When seeking anything but your most-used items and armor, roll 9 or less on a D20 to find it."),

        new("Drunkard",
            "Your drinking wavers between revelry and tomfoolery. Beer and wine drain your pockets and frustrate your friends."),

        new("Employed",
            "You are paid or oathsworn to a lord or employer. Disobey at your own peril!"),

        new("Greedy",
            "Tempted by treasure, roll 6 or less on a D20 to resist the urge."),

        new("Grudge",
            "You have been wronged, and hold it against an individual or group, unjustly. When you encounter them, you behave terribly."),

        new("Impetuous",
            "You are impatient, leaping into situations without planning or reservation. Only take action in phase 1 or 2."),

        new("Injured",
            "This common flaw should be taken a few times in a character's lifetime. An injury brings a -1 maximum to skill inventory."),

        new("Just a Kid",
            "Hey, I'm just a kid! Never exceed 6 skills."),

        new("Paranoid",
            "What's that? You hear that? You invent your own boogie men, and sow doubt among others. NPCs will be hesitant to trust you."),

        new("Phobia",
            "Fear of a common thing. In its presence, no roll can succeed."),

        new("Pursued",
            "You have a nemesis out there… somewhere… hunting you."),

        new("Sickly",
            "You were born frail. Survival and Resist skills cannot exceed 9."),

        new("Stubborn",
            "If you make up your mind, only a D20 contest with another player or NPC will sway you."),

        new("Timid",
            "You'd rather let others lead. Only take your action in phase 4 or 5."),

        new("Unlucky",
            "No matter how many rabbit's feet you carry, you have a penchant for comical mishaps and crit fail on a 16+."),

    }.AsReadOnly();
}
