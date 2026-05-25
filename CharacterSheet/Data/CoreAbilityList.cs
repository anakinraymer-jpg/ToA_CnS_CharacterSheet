namespace CharacterSheet.Data;

/// <summary>
/// All Crown &amp; Skull core abilities, alphabetically sorted.
/// Shares the <see cref="SkillEntry"/> record type so they can be displayed
/// in a SkillPickerBox via the EntrySource dependency property.
/// </summary>
public static class CoreAbilityList
{
    public static readonly IReadOnlyList<SkillEntry> All = new List<SkillEntry>
    {
        new("Battlemaster",
            "You have been trained as a soldier who knows how to hit hard. When you roll maximum damage, roll again. There is no limit to the number of times the dice can 'explode.'"),

        new("Brutal Fighter",
            "You are fast, efficient, and adept at taking on multiple opponents in close quarters. All of your melee attacks may hit 2 foes with a single damage roll."),

        new("Druid",
            "Master of the wilderness. Gain the Tracking and Hunting skills for free, each at 9. When outdoors, gain +3 on all skills."),

        new("Dumb Luck",
            "Every time it seems like all hope is lost, you stumble through unscathed. Must be a Proudfoot to gain this ability. To use, simply ask \"Am I lucky this time?\" and roll a D6. The GM will assist in your results."),

        new("Empath",
            "You have masterful intuition. Gain the Investigation, Streetwise and Oratory skills at a value of 7 for free."),

        new("Gnome",
            "You are one of the tiny folk… at home in caves and mossy gardens, sporting pointy hat and comical voice. Your nose is only slightly larger than your kind heart. In combat, enemies only see you if you attract their attention on purpose or with an attack."),

        new("Go Unnoticed",
            "You blend into shadows with ease. When enemies are attacking you and your allies, they will target you last."),

        new("Gunslinger",
            "Black powder weapons just make sense to you. When using your Black Powder skill to inflict maximum damage, take another shot (the bonus shot does not count as your first shot)."),

        new("Guru",
            "You are a wise creature who draws devotees and aspirants with your serene demeanor. Without intention, you bring light and optimism wherever you go. In every village or town you visit, gain 1D4 followers within 1 day."),

        new("Inventor",
            "Technology and gadgetry are a breeze for the likes of you: all but child's play to repair and create devices of all kinds. Use the Repair skill with no risk of breakage, and gain a 'Create Device' skill at the same value as Repair at no cost."),

        new("Loremaster",
            "You are an expert historian and keeper of myth. Pay half cost for any areas-of-knowledge skills you purchase."),

        new("Master of The Way",
            "You have an acute sense for the energy field that binds all living things. Must be Achaean to gain this ability. Gain the rare book 'L3la's Way' at no cost."),

        new("Metal",
            "You are made of alloy, rivets, and steel. Must be a droid to gain this ability. If a harmful effect indicates 'evade only' or ignores defense rolls for any reason, you may disregard that and use your Defense roll anyway. Dragonfire incoming? Just pull your head in and brace for impact!"),

        new("Mountaineer",
            "A master of finding ways through rock and cavern. Gain the Climber and Breakfall skills at 10. In mountains or underground, you also have a Scout skill of 15."),

        new("Paragon of Faith",
            "Your faith skill grows by 2 with a successful cast, and maxes at 16. Additionally, destroy 1d6 weak undead (such as zombies, shamblers or skeletons) as an Exhausting spell."),

        new("Protector",
            "When using a shield, change a failed Defense roll by an ally into a success once per round, if they are nearby."),

        new("Spellsinger",
            "You cast spells with flair and oratory. You can never use 'silent' or 'still' spells. As a default, though, your spells work on a target that can HEAR you cast. Upgrade from there as normal."),

        new("Uncanny Shot",
            "Roll an extra damage die with all ranged weapons, and ignore 1 DEF on all targets when using ranged weapons."),

        new("Veteran Commander",
            "When a combat encounter starts, choose 1 ally. Inspire them with a command to fight! Their first attack does maximum damage, and they cannot be harmed for the remainder of that round."),

        new("Wizard Savant",
            "You don't just cast spells; you are a conduit of incredible power. When casting, effects and outputs of all magic you cast are maximized on a Magic skill roll of 6 or less on a D20."),

    }.AsReadOnly();
}
