using System;
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

        internal BossEntry(string fight, string character, Levels level, bool isPlane, string image)
        {
            Fight = fight;
            Character = character;
            Level = level;
            IsPlane = isPlane;
            Image = image;
        }
    }

    internal sealed class EquipmentEntry<T>
    {
        internal readonly string Name;
        internal readonly T Value;
        internal readonly string Image;

        internal EquipmentEntry(string name, T value, string image)
        {
            Name = name;
            Value = value;
            Image = image;
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
            new BossEntry("PÃ¡nico BotÃ¡nico", "La pandilla raÃ­z", Levels.Veggies, false, "bosses/pandillaraiz.png"),
            new BossEntry("Treta en el LÃ©gamo", "Goopy Le Grande", Levels.Slime, false, "bosses/goopylegrande.png"),
            new BossEntry("Dirigible Temible", "Hilda Berg", Levels.FlyingBlimp, true, "bosses/hilda.png"),
            new BossEntry("Furia Floral", "Clavel de Cagney", Levels.Flower, false, "bosses/claveldecagney.png"),
            new BossEntry("OscilaciÃ³n GolosÃ­nica", "Baronesa Von Bon Bon", Levels.Baroness, false, "bosses/baronesa.png"),
            new BossEntry("Peligro Piramidal", "Djimmi el Grande", Levels.FlyingGenie, true, "bosses/djimmi.png"),
            new BossEntry("Karnaval Konfuso", "Beppi el Payaso", Levels.Clown, false, "bosses/beppi.png"),
            new BossEntry("AcciÃ³n Aviaria", "Titi Trinos", Levels.FlyingBird, true, "bosses/titi.png"),
            new BossEntry("Jolgorio Jocoso", "FÃ³sforo SombrÃ­o", Levels.Dragon, false, "bosses/fosforo.png"),
            new BossEntry("Heraldo del Panal", "Reynita Abejita", Levels.Bee, false, "bosses/abejita.png"),
            new BossEntry("Cuerpo de Roedores", "Werner Werman", Levels.Mouse, false, "bosses/werner.png"),
            new BossEntry("Disparar y Saquear", "CapitÃ¡n Barbasalada", Levels.Pirate, false, "bosses/capitan.png"),
            new BossEntry("Jugarretas MarÃ­timas", "Cala MarÃ­a", Levels.FlyingMermaid, true, "bosses/calamaria.png"),
            new BossEntry("Obra Intensita", "Sally Teatral", Levels.SallyStagePlay, false, "bosses/sally.png"),
            new BossEntry("Jive del Basurero", "Robot del Dr. Kahl", Levels.Robot, true, "bosses/robot.png"),
            new BossEntry("Furia en las VÃ­as", "Expreso Fantasma", Levels.Train, false, "bosses/expreso.png"),
            new BossEntry("Â¡Apuestas Cerradas!", "Rey Dado", Levels.DicePalaceMain, false, "bosses/dado.png"),
            new BossEntry("DiversiÃ³n Infernal", "El Diablo", Levels.Devil, false, "bosses/diablo.png"),
            new BossEntry("Cubil del Contrabando", "Las AlimaÃ±as", Levels.RumRunners, false, "bosses/alimanas.png"),
            new BossEntry("Morradas a MediodÃ­a", "Esther Espuelas", Levels.FlyingCowboy, true, "bosses/vaca.png"),
            new BossEntry("Perreo Perriagudo", "Los Perritos Pilotos", Levels.Airplane, false, "bosses/perritos.png"),
            new BossEntry("Jefe Secreto", "Ãngel y Demonio", Levels.Graveyard, false, "bosses/angelydemonio.png"),
            new BossEntry("Sopapos Sectafilocopos", "Genovevo de Gelante", Levels.SnowCult, false, "bosses/genovevo.png"),
            new BossEntry("Altercado AgrÃ³gnomo", "Granitoviejo el Gigante", Levels.OldMan, false, "bosses/granito.png"),
            new BossEntry("Un menÃº que te mueres", "Chef Saleroso", Levels.Saltbaker, false, "bosses/salero.png")
        };

        internal static readonly EquipmentEntry<Weapon>[] Weapons =
        {
            new EquipmentEntry<Weapon>("Lanzaguisantes", Weapon.level_weapon_peashot, "weapons/lanzaguisantes.png"),
            new EquipmentEntry<Weapon>("ExpansiÃ³n", Weapon.level_weapon_spreadshot, "weapons/expansion.png"),
            new EquipmentEntry<Weapon>("Rastreador", Weapon.level_weapon_homing, "weapons/rastreador.png"),
            new EquipmentEntry<Weapon>("Globero", Weapon.level_weapon_bouncer, "weapons/globero.png"),
            new EquipmentEntry<Weapon>("Carga", Weapon.level_weapon_charge, "weapons/carga.png"),
            new EquipmentEntry<Weapon>("Rodeo", Weapon.level_weapon_boomerang, "weapons/rodeo.png"),
            new EquipmentEntry<Weapon>("Tiro Certero", Weapon.level_weapon_crackshot, "weapons/tirocertero.png"),
            new EquipmentEntry<Weapon>("Convergencia", Weapon.level_weapon_wide_shot, "weapons/convergencia.png"),
            new EquipmentEntry<Weapon>("CiclÃ³nica", Weapon.level_weapon_upshot, "weapons/ciclonica.png"),
            new EquipmentEntry<Weapon>("Nada", Weapon.None, "weapons/vacio.png")
        };

        internal static readonly EquipmentEntry<Super>[] Supers =
        {
            new EquipmentEntry<Super>("SÃºper I", Super.level_super_beam, "supers/super1.png"),
            new EquipmentEntry<Super>("SÃºper II", Super.level_super_invincible, "supers/super2.png"),
            new EquipmentEntry<Super>("SÃºper III", Super.level_super_ghost, "supers/super3.png"),
            new EquipmentEntry<Super>("Nada", Super.None, "weapons/vacio.png")
        };

        internal static readonly EquipmentEntry<Charm>[] Charms =
        {
            new EquipmentEntry<Charm>("CorazÃ³n", Charm.charm_health_up_1, "charms/corazon.png"),
            new EquipmentEntry<Charm>("CafÃ©", Charm.charm_super_builder, "charms/cafe.png"),
            new EquipmentEntry<Charm>("Bomba de humo", Charm.charm_smoke_dash, "charms/bombadehumo.png"),
            new EquipmentEntry<Charm>("DesvÃ­o Dulce", Charm.charm_parry_plus, "charms/desviodulce.png"),
            new EquipmentEntry<Charm>("CorazÃ³n Doble", Charm.charm_health_up_2, "charms/corazondoble.png"),
            new EquipmentEntry<Charm>("Afiladora", Charm.charm_parry_attack, "charms/afiladora.png"),
            new EquipmentEntry<Charm>("Galletita Astral", Charm.charm_chalice, "charms/galletitaastral.png"),
            new EquipmentEntry<Charm>("Reliquia Divina", Charm.charm_curse, "charms/reliquiadivina.png"),
            new EquipmentEntry<Charm>("Anillo de CorazÃ³n", Charm.charm_healer, "charms/anillocorazon.png"),
            new EquipmentEntry<Charm>("Nada", Charm.None, "weapons/vacio.png")
        };

        internal static readonly ModifierEntry[] Modifiers =
        {
            new ModifierEntry("No Dash", ModifierKind.Ground, "modifiers/nodash.png"),
            new ModifierEntry("No mini aviÃ³n", ModifierKind.Plane, "modifiers/nomini.png"),
            new ModifierEntry("Solo mini aviÃ³n", ModifierKind.Plane, "modifiers/miniavion.png"),
            new ModifierEntry("No disparo bombas", ModifierKind.Plane, "modifiers/nobombitas.png"),
            new ModifierEntry("No disparo Peashooter", ModifierKind.Plane, "modifiers/nopeashooterair.png"),
            new ModifierEntry("No EX", ModifierKind.Both, "modifiers/noex.png"),
            new ModifierEntry("Nada", ModifierKind.Both, "weapons/vacio.png")
        };

        internal static List<int> ValidModifierIndices(BossEntry boss)
        {
            var result = new List<int>();
            for (var i = 0; i < Modifiers.Length; i++)
            {
                var modifier = Modifiers[i];
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

