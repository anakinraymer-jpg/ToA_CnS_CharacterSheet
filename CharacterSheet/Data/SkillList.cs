namespace CharacterSheet.Data;

public record SkillEntry(string Name, string Description);

public static class SkillList
{
    public static readonly IReadOnlyList<SkillEntry> All = new List<SkillEntry>
    {
        new("Alchemy",
            "You are skilled with chemicals, powders, reductions, elixirs and oils. With an Alchemist's kit (glass bells, candle, mixing pot), a day's time in safety, and this skill roll, you can create 1D4 flasks of a specified type. Your GM will referee the effects and use of these flasks upon creation."),

        new("Animal Training",
            "Charm or induce an animal companion to perform simple actions. Can only be used with one animal at a time."),

        new("Armorer",
            "In arm's length of an ally, roll this skill to repair 1 damaged armor item in their inventory. On a failure, the armor is destroyed."),

        new("Art",
            "You have spent years mastering a specific technique of painting, sculpture, pottery, or drawing. With this skill, you can entrance passers-by, sell pieces for good coin, or be patronized by nobles."),

        new("Beast Codex",
            "You have a keen eye for foes encountered. With this skill, recall all details of a foe already-fought when encountered again."),

        new("Black Powder",
            "You are an expert with firearms. Reload instantly, keep powder safe in water, or ignore a critical firearm failure with this skill. Additionally, on the first firearm attack you make in an encounter, roll this skill to inflict maximum damage. A perfect balance of powder and shot."),

        new("Brawling",
            "Fight with fists and feet. Two uses: 1) Roll the skill to grapple or stun a foe for 1 round. 2) Use a D6 when attacking without a weapon."),

        new("Breakfall",
            "Half damage taken in long falls if this skill rolled successfully."),

        new("Cartographer",
            "You are an expert with maps. Make reliable, accurate maps and estimates of areas with this skill."),

        new("Climber",
            "Climb any reasonable surface with texture, holds, or crevasses."),

        new("Courtier",
            "Roll to outwit bureaucrats or royalty."),

        new("Disguise",
            "Take on alternative identities. More difficult with acquaintances."),

        new("Draconic Speech",
            "Roll this skill to effectively and politely talk to dragons."),

        new("Dragon Lore",
            "This specific knowledge-type skill can carry a special role. If employing this skill to effect, the GM should prevent any players without this skill from reading the 'Lineage of Dragons' chapter at any time. On the other hand, a hero with this skill may keep that chapter at the ready during play. Roll to leverage such use of the book during play."),

        new("Dragon Riding",
            "Roll to hang on or make maneuvers with a dragon mount."),

        new("Dual Wield",
            "You prefer a melee weapon in each hand, abandoning defensive thinking for hacking and chopping with speed and skill. When you attack this way, roll your first weapon as normal. Then roll this skill. Inflict the margin of success with that roll as the damage of the second weapon. Even a small weapon such as a dagger can be effective here, helping to mitigate the cost of improving the Dual Wield skill."),

        new("Evade",
            "Used to avoid attacks that armor cannot protect against."),

        new("Faith",
            "The ability to call forth miracles with prayer (see Divine Magic)."),

        new("Fishing",
            "Usable at any water source to secure food. If a cooking fire is available, add 1 to any form of recovery, be it 'Take a Breather' or full rest."),

        new("Forage",
            "A skill used to find plants in the Herbalists' Guide."),

        new("Gambling",
            "Cheat or overcome casino games with devious attention."),

        new("Hunting",
            "Acquire food in natural environments, enough for a group."),

        new("Investigate",
            "Uncover hidden information, history or subtle facts."),

        new("Jump",
            "Leap with uncanny agility, out-jumping the untrained by 3-fold."),

        new("Knowledge",
            "Roll to recall details on a single topic or area of expertise."),

        new("Languages",
            "Be fluent with a successful roll. Only spend here for languages outside what would be normal for your folk to speak."),

        new("Librarian",
            "You are a student, caretaker, collector and custodian of books. Roll this skill to copy, authenticate, assemble, or recall the history of any books or bookcraft."),

        new("Linguist",
            "Discern lost or unknown language, glyphs, or writing."),

        new("Lockpicking",
            "Disable non-magical locking devices with tools."),

        new("Magic",
            "Used to contain infernal or unstable magic, or as a resistance to spells. Also used for wild magic and anti-magic."),

        new("Medical",
            "Recover a crossed-off skill on yourself or an ally, with a touch. A failed roll renders that skill recoverable only by rest."),

        new("Mining",
            "Navigate or excavate underground with perfect direction and skill."),

        new("Muscle",
            "You have a knack for leverage, a powerful grip, and raw strength. Roll this skill to bend bars, lift the unliftable, or brace against wind."),

        new("Oratory",
            "Fascinate or deceive with stories, bravado and sheer charisma."),

        new("Parry",
            "As a defensive roll when attacked, use Parry to deflect or reflect single melee or ranged attacks back at their source."),

        new("Pickpocket",
            "Acquire small or simple items on unsuspecting targets."),

        new("Profession",
            "Any working skill or commerce skill, its tools and ways."),

        new("Quickdraw",
            "You have a fast hand with ranged weapons. When an engagement starts, the GM calling for 'Roll to go first,' you may intervene by announcing then rolling this skill. On a success, you may snap off a shot, and call what phase combat begins, making that your phase henceforth for the encounter."),

        new("Repair",
            "Mend a damaged weapon or item. Not usable on armor. A failed roll destroys the item. \"I think I dropped a piece…\""),

        new("Resist",
            "You are hearty. Roll to ignore extreme cold, poison, or too many mugs of ale. Hold your breath or fight back harmful magic with Resist."),

        new("Resist (Type)",
            "A more specific resistance to fit character concept or origin. Purchase this skill at half normal price (½ point for +1), but specify a very specific type such as cold, poison, holding your breath, or heat."),

        new("Riding",
            "Perform daring maneuvers on horseback without penalties."),

        new("Running",
            "Run double the distance of others, outrun pursuers in the open."),

        new("Sailing",
            "Operate marine vessels, with appropriate crew."),

        new("Salvage",
            "Just junk? Not to you. Roll this skill to harvest 1 three-point item from a fallen enemy. Use of salvage requires a full turn."),

        new("Scholar",
            "Achieve renown in a specific topic. You are published."),

        new("Scout",
            "Find all kinds of clues, information, or traps in the immediate area."),

        new("Scrivener",
            "Use this skill to copy maps, scrolls, or documents. Also used to create accurate and lifelike illustrations, portraits, or scientific drawings."),

        new("Shield Fighting",
            "When using a shield, roll this skill instead of Defense. Additionally, add the defense bonus of your shield to this skill."),

        new("Skinning",
            "Harvest a slain beast for pelt or hide in 1D4 rounds."),

        new("Spell Research",
            "Roll to study spells with time and materials in a safe place. Gain 1 hero point per day invested thus, spendable only on spells."),

        new("Starships",
            "You are familiar with the workings and operations of starfaring ships. You can launch and land, repair, and identify them with this skill."),

        new("Stealth",
            "Go unnoticed when moving. Easier if perfectly still or hidden."),

        new("Streetwise",
            "You know shady people and back-alley secrets."),

        new("Survival",
            "If your heart is hit, a lethal injury, roll this skill instantly (it will be crossed off). On a success, you fight off death's embrace for 1 round. Also rolled when refusing to rest and pushing onward after lack of sleep."),

        new("Swimming",
            "Swim twice as far as others. Stay underwater twice as long."),

        new("Tactics",
            "You are skilled at the analysis and exploitation of enemy action. Roll this skill on your turn as an action. With success in your analysis, the GM will roll upcoming enemy tactics immediately. These results can be acted on, by you and allies, in the phases between the Tactics skill roll and the tactics so rolled."),

        new("Take Aim",
            "Add a die of your attack type on the next attack you make."),

        new("Technology",
            "With the discovery of the Exodur, strange technology has come to The North Holds, along with the clockworks of the Goldpine. Use this skill to modify, build, repair, or comprehend such technology when found."),

        new("Tracking",
            "Find obscured foes in the wild."),

        new("Trading",
            "Get more value out of trade despite hagglers, bad leverage, distractions or scammers."),

        new("Traps",
            "The trap artisan specializes in delayed-effect snares, alarms, or deadly tripwire effects. Roll this skill to carefully set a trap (requires 1D4 turns to assemble). The trap inflicts its desired effect on one unwitting victim: D12 damage or 3 attrition, immobilize, or ring an alarm. This skill can also be rolled to detect or disable such devices."),
    }.AsReadOnly();

    private static readonly Dictionary<string, string> _lookup =
        All.ToDictionary(s => s.Name, s => s.Description, StringComparer.OrdinalIgnoreCase);

    public static string? GetDescription(string? name)
        => name != null && _lookup.TryGetValue(name, out var d) ? d : null;
}
