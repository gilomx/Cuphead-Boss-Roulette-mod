using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal enum ModifierKind
    {
        Ground,
        Plane,
        Both
    }

    internal sealed class BossEntry
    {
        internal readonly string Fight;
        internal readonly string Character;
        internal readonly Levels Level;
        internal readonly bool IsPlane;
        internal readonly string Image;
        internal readonly bool RequiresDlc;

        internal BossEntry(string fight, string character, Levels level,
            bool isPlane, string image, bool requiresDlc = false)
        {
            Fight = fight;
            Character = character;
            Level = level;
            IsPlane = isPlane;
            Image = image;
            RequiresDlc = requiresDlc;
        }
    }

    internal sealed class EquipmentEntry<T>
    {
        internal const int NoCurseLevelOverride = -1;

        internal readonly string Name;
        internal readonly T Value;
        internal readonly string Image;
        internal readonly string NativeSprite;
        internal readonly bool RequiresDlc;
        internal readonly int CurseLevelOverride;

        internal EquipmentEntry(string name, T value, string image,
            string nativeSprite, bool requiresDlc = false,
            int curseLevelOverride = NoCurseLevelOverride)
        {
            Name = name;
            Value = value;
            Image = image;
            NativeSprite = nativeSprite;
            RequiresDlc = requiresDlc;
            CurseLevelOverride = curseLevelOverride;
        }
    }

    internal sealed class ModifierEntry
    {
        internal readonly string Name;
        internal readonly ModifierKind Kind;
        internal readonly string Image;

        internal ModifierEntry(string name, ModifierKind kind, string image)
        {
            Name = name;
            Kind = kind;
            Image = image;
        }
    }

    internal sealed class RouletteResult
    {
        internal int Boss;
        internal int Weapon1;
        internal int Weapon2;
        internal int Super;
        internal int Charm;
        internal int Modifier;
    }

    internal static class RouletteData
    {
        internal static readonly BossEntry[] Bosses =
        {
            new BossEntry("Calamidad Conjunta", "Hosco y Tosco", Levels.Frogs, false, "bosses/hoscoytosco.png"),
            new BossEntry("Pánico Botánico", "La pandilla raíz", Levels.Veggies, false, "bosses/pandillaraiz.png"),
            new BossEntry("Treta en el Légamo", "Goopy Le Grande", Levels.Slime, false, "bosses/goopylegrande.png"),
            new BossEntry("Dirigible Temible", "Hilda Berg", Levels.FlyingBlimp, true, "bosses/hilda.png"),
            new BossEntry("Furia Floral", "Clavel de Cagney", Levels.Flower, false, "bosses/claveldecagney.png"),
            new BossEntry("Oscilación Golosínica", "Baronesa Von Bon Bon", Levels.Baroness, false, "bosses/baronesa.png"),
            new BossEntry("Peligro Piramidal", "Djimmi el Grande", Levels.FlyingGenie, true, "bosses/djimmi.png"),
            new BossEntry("Karnaval Konfuso", "Beppi el Payaso", Levels.Clown, false, "bosses/beppi.png"),
            new BossEntry("Acción Aviaria", "Titi Trinos", Levels.FlyingBird, true, "bosses/titi.png"),
            new BossEntry("Jolgorio Jocoso", "Fósforo Sombrío", Levels.Dragon, false, "bosses/fosforo.png"),
            new BossEntry("Heraldo del Panal", "Reynita Abejita", Levels.Bee, false, "bosses/abejita.png"),
            new BossEntry("Cuerpo de Roedores", "Werner Werman", Levels.Mouse, false, "bosses/werner.png"),
            new BossEntry("Disparar y Saquear", "Capitán Barbasalada", Levels.Pirate, false, "bosses/capitan.png"),
            new BossEntry("Jugarretas Marítimas", "Cala María", Levels.FlyingMermaid, true, "bosses/calamaria.png"),
            new BossEntry("Obra Intensita", "Sally Teatral", Levels.SallyStagePlay, false, "bosses/sally.png"),
            new BossEntry("Jive del Basurero", "Robot del Dr. Kahl", Levels.Robot, true, "bosses/robot.png"),
            new BossEntry("Furia en las Vías", "Expreso Fantasma", Levels.Train, false, "bosses/expreso.png"),
            new BossEntry("¡Apuestas Cerradas!", "Rey Dado", Levels.DicePalaceMain, false, "bosses/dado.png"),
            new BossEntry("Diversión Infernal", "El Diablo", Levels.Devil, false, "bosses/diablo.png"),
            new BossEntry("Cubil del Contrabando", "Las Alimañas", Levels.RumRunners, false, "bosses/alimanas.png", true),
            new BossEntry("Morradas a Mediodía", "Esther Espuelas", Levels.FlyingCowboy, true, "bosses/vaca.png", true),
            new BossEntry("Perreo Perriagudo", "Los Perritos Pilotos", Levels.Airplane, false, "bosses/perritos.png", true),
            new BossEntry("Jefe Secreto", "Ángel y Demonio", Levels.Graveyard, false, "bosses/angelydemonio.png", true),
            new BossEntry("Sopapos Sectafilocopos", "Genovevo de Gelante", Levels.SnowCult, false, "bosses/genovevo.png", true),
            new BossEntry("Altercado Agrógnomo", "Granitoviejo el Gigante", Levels.OldMan, false, "bosses/granito.png", true),
            new BossEntry("Un menú que te mueres", "Chef Saleroso", Levels.Saltbaker, false, "bosses/salero.png", true)
        };

        internal static readonly EquipmentEntry<Weapon>[] Weapons =
        {
            new EquipmentEntry<Weapon>("Lanzaguisantes", Weapon.level_weapon_peashot, "weapons/lanzaguisantes.png", "equip_icon_weapon_peashot_0001"),
            new EquipmentEntry<Weapon>("Expansión", Weapon.level_weapon_spreadshot, "weapons/expansion.png", "equip_icon_weapon_spread_0001"),
            new EquipmentEntry<Weapon>("Rastreador", Weapon.level_weapon_homing, "weapons/rastreador.png", "equip_icon_weapon_homing_0001"),
            new EquipmentEntry<Weapon>("Globero", Weapon.level_weapon_bouncer, "weapons/globero.png", "equip_icon_weapon_bouncer_0001"),
            new EquipmentEntry<Weapon>("Carga", Weapon.level_weapon_charge, "weapons/carga.png", "equip_icon_weapon_charge_0001"),
            new EquipmentEntry<Weapon>("Rodeo", Weapon.level_weapon_boomerang, "weapons/rodeo.png", "equip_icon_weapon_boomerang_0001"),
            new EquipmentEntry<Weapon>("Tiro Certero", Weapon.level_weapon_crackshot, "weapons/tirocertero.png", "equip_icon_weapon_crackshot_0001", true),
            new EquipmentEntry<Weapon>("Convergencia", Weapon.level_weapon_wide_shot, "weapons/convergencia.png", "equip_icon_weapon_wide_shot_0001", true),
            new EquipmentEntry<Weapon>("Ciclónica", Weapon.level_weapon_upshot, "weapons/ciclonica.png", "equip_icon_weapon_upshot_0001", true),
            new EquipmentEntry<Weapon>("Nada", Weapon.None, "weapons/vacio.png", "equip_icon_empty")
        };

        internal static readonly EquipmentEntry<Super>[] Supers =
        {
            new EquipmentEntry<Super>("Súper I", Super.level_super_beam, "supers/super1.png", "equip_icon_super_beam_0001"),
            new EquipmentEntry<Super>("Súper II", Super.level_super_invincible, "supers/super2.png", "equip_icon_super_invincible_0001"),
            new EquipmentEntry<Super>("Súper III", Super.level_super_ghost, "supers/super3.png", "equip_icon_super_ghost_0001"),
            new EquipmentEntry<Super>("Nada", Super.None, "weapons/vacio.png", "equip_icon_empty")
        };

        internal static readonly EquipmentEntry<Charm>[] Charms =
        {
            new EquipmentEntry<Charm>("Corazón", Charm.charm_health_up_1, "charms/corazon.png", "equip_icon_charm_hp1_0001"),
            new EquipmentEntry<Charm>("Café", Charm.charm_super_builder, "charms/cafe.png", "equip_icon_charm_coffee_0001"),
            new EquipmentEntry<Charm>("Bomba de humo", Charm.charm_smoke_dash, "charms/bombadehumo.png", "equip_icon_charm_smoke-dash_0001"),
            new EquipmentEntry<Charm>("Desvío Dulce", Charm.charm_parry_plus, "charms/desviodulce.png", "equip_icon_charm_parry_slapper_0001"),
            new EquipmentEntry<Charm>("Corazón Doble", Charm.charm_health_up_2, "charms/corazondoble.png", "equip_icon_charm_hp2_0001"),
            new EquipmentEntry<Charm>("Afiladora", Charm.charm_parry_attack, "charms/afiladora.png", "equip_icon_charm_parry_attack_0001"),
            new EquipmentEntry<Charm>("Galletita Astral", Charm.charm_chalice, "charms/galletitaastral.png", "equip_icon_charm_chalice_0001", true),
            new EquipmentEntry<Charm>("Reliquia Maldita", Charm.charm_curse, "charms/reliquiadivina.png", "equip_icon_charm_curse_1_0001", true, 0),
            new EquipmentEntry<Charm>("Reliquia Divina", Charm.charm_curse, "charms/reliquiadivina.png", "equip_icon_charm_curse_5_0001", true, 4),
            new EquipmentEntry<Charm>("Anillo de Corazón", Charm.charm_healer, "charms/anillocorazon.png", "equip_icon_charm_healer_0001", true),
            new EquipmentEntry<Charm>("Nada", Charm.None, "weapons/vacio.png", "equip_icon_empty")
        };

        internal static readonly ModifierEntry[] Modifiers =
        {
            new ModifierEntry("No Dash", ModifierKind.Ground, "modifiers/nodash_01.png"),
            new ModifierEntry("No mini avión", ModifierKind.Plane, "modifiers/nomini_01.png"),
            new ModifierEntry("Solo mini avión", ModifierKind.Plane, "modifiers/mini_01.png"),
            new ModifierEntry("No disparo bombas", ModifierKind.Plane, "modifiers/nobombs_01.png"),
            new ModifierEntry("No disparo Peashooter", ModifierKind.Plane, "modifiers/nopeashooter_01.png"),
            new ModifierEntry("No EX", ModifierKind.Both, "modifiers/noex_01.png"),
            new ModifierEntry("Blanco y negro", ModifierKind.Both, "modifiers/blacknwhite_01.png"),
            new ModifierEntry("Nada", ModifierKind.Both, "weapons/vacio.png")
        };

        internal static List<int> ValidModifierIndices(BossEntry boss)
        {
            var result = new List<int>();
            for (var i = 0; i < Modifiers.Length; i++)
            {
                var modifier = Modifiers[i];
                if (modifier.Name == "Nada")
                    continue;
                if (modifier.Kind == ModifierKind.Both ||
                    (boss.IsPlane && modifier.Kind == ModifierKind.Plane) ||
                    (!boss.IsPlane && modifier.Kind == ModifierKind.Ground))
                {
                    result.Add(i);
                }
            }
            return result;
        }
    }
}
