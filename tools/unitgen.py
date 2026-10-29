#!/usr/bin/env python3
"""
Generates data/units/<faction>/*.json from the tables below.

The JSON files are what the game loads. This script is just the quickest way to keep two
factions with ~40 units each consistent: edit a number here, run `python3 tools/unitgen.py`,
and commit both. Rates are per second; the sim converts them to per tick.

Faction identity (see the design doc):
  Lattice  - precision, range, information: longer range and sight, faster, a bit more fragile
  Crucible - armour, attrition, salvage: tougher, slower, shorter range, better at reclaim
"""
import json
import os
import shutil

ROOT = os.path.join(os.path.dirname(__file__), "..", "data", "units")

FACTIONS = {
    "lattice": dict(hp=0.95, speed=1.08, range=1.04, sight=1.06, reclaim=1.0),
    "crucible": dict(hp=1.2, speed=0.92, range=0.97, sight=1.0, reclaim=1.4),
}

# key: (lattice name, crucible name)
NAMES = {
    "commander": ("Lattice Commander", "Crucible Commander"),
    "builder_bot": ("Surveyor", "Rigger"),
    "scout_bot": ("Glint", "Spark"),
    "assault_bot": ("Needle", "Brawler"),
    "aa_bot": ("Halo", "Cinder"),
    "mortar_bot": ("Mortar Walker", "Pounder"),
    "builder_vehicle": ("Pathfinder", "Dozer"),
    "tank": ("Strider", "Anvil"),
    "flak_vehicle": ("Prism", "Flak Truck"),
    "artillery_vehicle": ("Lancer Array", "Howitzer Cart"),
    "salvager": ("Gleaner", "Scrapper"),
    "builder_air": ("Kite", "Hauler"),
    "fighter": ("Swift", "Hornet"),
    "bomber": ("Lancet", "Anvilfall"),
    "gunship": ("Seraph", "Buzzsaw"),
    "builder_sea": ("Tender", "Dredger"),
    "patrol_boat": ("Skiff", "Cutter"),
    "destroyer": ("Reef", "Hammerhead"),
    "battleship": ("Monolith", "Leviathan"),
    "builder_adv": ("Architect", "Foreman"),
    "heavy_assault": ("Arbiter", "Bulwark"),
    "long_artillery": ("Longbow", "Siegebreaker"),
    "amphibious": ("Wader", "Mudskipper"),
    "experimental": ("Skywarden", "Mantle Crawler"),
    "metal_extractor": ("Metal Extractor", "Metal Extractor"),
    "solar_collector": ("Solar Collector", "Solar Collector"),
    "wind_turbine": ("Wind Turbine", "Wind Turbine"),
    "metal_storage": ("Metal Storage", "Metal Storage"),
    "energy_storage": ("Energy Storage", "Energy Storage"),
    "metal_maker": ("Metal Maker", "Metal Maker"),
    "bot_lab": ("Bot Lab", "Foundry"),
    "vehicle_plant": ("Vehicle Plant", "Tread Works"),
    "aircraft_plant": ("Skyport", "Hangar"),
    "shipyard": ("Shipyard", "Drydock"),
    "light_turret": ("Sentinel", "Brander"),
    "aa_turret": ("Spire", "Flak Nest"),
    "radar_tower": ("Oracle Mast", "Watchtower"),
    "deep_extractor": ("Deep Extractor", "Deep Bore"),
    "fusion_reactor": ("Fusion Reactor", "Fusion Furnace"),
    "heavy_turret": ("Bastion", "Rampart"),
    "plasma_battery": ("Lance Battery", "Slag Battery"),
    "advanced_assembly": ("Advanced Assembly", "Heavy Works"),
    "adv_bot_lab": ("Synthesis Lab", "Great Foundry"),
    "adv_aircraft_plant": ("High Skyport", "Great Hangar"),
    "adv_shipyard": ("Fleetyard", "Graving Dock"),
    "builder_adv_bot": ("Warden", "Sapper"),
    "heavy_bot": ("Paladin", "Ironclad"),
    "sniper_bot": ("Marksman", "Rivet Gun"),
    "missile_tank": ("Volley", "Hailstorm"),
    "builder_adv_air": ("Overseer", "Skycrane"),
    "heavy_bomber": ("Eclipse", "Thunderhead"),
    "interceptor": ("Falcon", "Wasp"),
    "builder_adv_sea": ("Shipwright", "Salvage Barge"),
    "cruiser": ("Tidewarden", "Ironside"),
    "missile_ship": ("Starfall", "Broadside"),
    "experimental_gantry": ("Experimental Gantry", "Colossus Gantry"),
}

# Older ids kept so existing tests, replays and docs still line up.
ID_OVERRIDES = {
    ("lattice", "builder_bot"): "lattice_surveyor",
    ("crucible", "builder_bot"): "crucible_rigger",
    ("lattice", "tank"): "lattice_strider",
    ("crucible", "tank"): "crucible_anvil",
}

T1_STRUCTURES = ["metal_extractor", "solar_collector", "wind_turbine", "metal_storage", "energy_storage",
                 "metal_maker", "bot_lab", "vehicle_plant", "aircraft_plant", "shipyard",
                 "light_turret", "aa_turret", "radar_tower"]
T2_STRUCTURES = ["deep_extractor", "fusion_reactor", "heavy_turret", "plasma_battery", "experimental_gantry"]

FACTORY_OPTIONS = {
    "bot_lab": ["builder_bot", "scout_bot", "assault_bot", "aa_bot", "mortar_bot"],
    "vehicle_plant": ["builder_vehicle", "tank", "flak_vehicle", "artillery_vehicle", "salvager"],
    "aircraft_plant": ["builder_air", "fighter", "bomber", "gunship"],
    "shipyard": ["builder_sea", "patrol_boat", "destroyer"],
}
# Advanced factories are refits of a tier 1 factory: they keep its whole list and add tier 2.
FACTORY_OPTIONS.update({
    "adv_bot_lab": FACTORY_OPTIONS["bot_lab"] + ["builder_adv_bot", "heavy_bot", "sniper_bot", "amphibious"],
    "advanced_assembly": FACTORY_OPTIONS["vehicle_plant"] + ["builder_adv", "heavy_assault", "long_artillery", "missile_tank"],
    "adv_aircraft_plant": FACTORY_OPTIONS["aircraft_plant"] + ["builder_adv_air", "interceptor", "heavy_bomber"],
    "adv_shipyard": FACTORY_OPTIONS["shipyard"] + ["builder_adv_sea", "cruiser", "missile_ship", "battleship"],
    "experimental_gantry": ["experimental"],
})

COMMANDER_OPTIONS = T1_STRUCTURES
# Each tier 1 builder can refit only its own kind of factory (a bot builder makes the advanced bot lab).
BUILDER_OPTIONS = {
    "bot": T1_STRUCTURES + ["adv_bot_lab"],
    "vehicle": T1_STRUCTURES + ["advanced_assembly"],
    "air": T1_STRUCTURES + ["adv_aircraft_plant"],
    "sea": T1_STRUCTURES + ["adv_shipyard"],
}
# Advanced builders specialise, so a full tier 2 base needs more than one branch:
#   vehicle  - industry: fusion, deep extractors
#   bot      - fortification: heavy turrets, plasma batteries
#   air      - a bit of everything, at low build power
#   sea      - industry plus coastal heavy turrets
_BASICS = ["metal_extractor", "solar_collector", "radar_tower", "metal_maker", "metal_storage", "energy_storage"]
ADV_BUILDER_OPTIONS = {
    "vehicle": ["deep_extractor", "fusion_reactor", "experimental_gantry", "advanced_assembly"] + _BASICS,
    "bot": ["heavy_turret", "plasma_battery", "light_turret", "aa_turret", "experimental_gantry", "adv_bot_lab"] + _BASICS,
    "air": ["deep_extractor", "fusion_reactor", "heavy_turret", "plasma_battery", "experimental_gantry",
            "adv_aircraft_plant"] + _BASICS,
    "sea": ["deep_extractor", "fusion_reactor", "heavy_turret", "adv_shipyard"] + _BASICS,
}


def laser(rng, dmg, reload, acc=0.9, air=False, ground=True):
    return dict(kind="laser", range=rng, damage=dmg, reloadSeconds=reload, accuracy=acc,
                targetsAir=air, targetsGround=ground)


def cannon(rng, dmg, reload, acc=0.85, splash=1.5, speed=70):
    return dict(kind="cannon", range=rng, damage=dmg, reloadSeconds=reload, accuracy=acc,
                splash=splash, projectileSpeed=speed)


def artillery(rng, dmg, reload, splash, acc=0.7, min_range=10, speed=45):
    return dict(kind="artillery", range=rng, minRange=min_range, damage=dmg, reloadSeconds=reload,
                accuracy=acc, splash=splash, projectileSpeed=speed)


def missile_aa(rng, dmg, reload, speed=90):
    return dict(kind="missile", range=rng, damage=dmg, reloadSeconds=reload, accuracy=0.95,
                projectileSpeed=speed, targetsAir=True, targetsGround=False)


def flak(rng, dmg, reload):
    return dict(kind="flak", range=rng, damage=dmg, reloadSeconds=reload, accuracy=0.9,
                splash=2, targetsAir=True, targetsGround=False)


def bomb(dmg, reload, splash):
    return dict(kind="bomb", range=7, damage=dmg, reloadSeconds=reload, accuracy=0.9, splash=splash,
                projectileSpeed=20)


def dgun():
    return dict(kind="dgun", range=20, damage=12000, reloadSeconds=1.2, accuracy=1, splash=3,
                projectileSpeed=32, energyPerShot=500)


# Base stats. Mobile: speed in units/s, radius in units. Structures: footprint in 8-unit tiles.
UNITS = {
    # ---- Commander -------------------------------------------------------------------------
    "commander": dict(role="commander", commander=True, tier=1, metalCost=2500, energyCost=25000, buildTime=60000,
                      health=3200, moveClass="amphibious", speed=3.4, radius=3.2, sightRange=34,
                      radarRange=0, buildPower=150, buildRange=14, metalIncome=1.5, energyIncome=25,
                      metalStorage=500, energyStorage=500,
                      weapons=[laser(16, 70, 0.6, 0.95), dgun()],
                      deathExplosionDamage=3000, deathExplosionRadius=26, wreckFraction=0.5,
                      buildOptions=COMMANDER_OPTIONS,
                      description="Builds your first base and carries the Breach Cannon. Lose it and you lose the war."),

    # ---- Bot Lab -----------------------------------------------------------------------------
    "builder_bot": dict(role="builder_bot", tier=1, metalCost=110, energyCost=1300, buildTime=1500,
                        health=380, moveClass="bot", speed=4.6, radius=1.6, sightRange=24,
                        buildPower=80, buildRange=12, buildOptions=BUILDER_OPTIONS["bot"],
                        description="Walking constructor. Climbs hills vehicles can't."),
    "scout_bot": dict(role="scout_bot", tier=1, metalCost=30, energyCost=450, buildTime=450,
                      health=110, moveClass="bot", speed=9.5, radius=1.2, sightRange=40,
                      weapons=[laser(10, 9, 0.45, 0.7)],
                      description="Fast and fragile. Sees far."),
    "assault_bot": dict(role="assault_bot", tier=1, metalCost=60, energyCost=700, buildTime=800,
                        health=270, moveClass="bot", speed=5.8, radius=1.4, sightRange=26,
                        weapons=[laser(14, 17, 0.6, 0.85)],
                        description="Cheap laser infantry. Good in numbers."),
    "aa_bot": dict(role="aa_bot", tier=1, metalCost=80, energyCost=950, buildTime=950,
                   health=260, moveClass="bot", speed=5, radius=1.4, sightRange=28,
                   weapons=[missile_aa(28, 45, 1.3)],
                   description="Anti-air missiles. Can't hit the ground."),
    "mortar_bot": dict(role="mortar_bot", tier=1, metalCost=140, energyCost=1600, buildTime=1600,
                       health=300, moveClass="bot", speed=4, radius=1.6, sightRange=26,
                       weapons=[artillery(38, 75, 3.0, 4, min_range=8)],
                       description="Lobs shells over walls and hills."),

    # ---- Vehicle Plant -----------------------------------------------------------------------
    "builder_vehicle": dict(role="builder_vehicle", tier=1, metalCost=150, energyCost=1700, buildTime=1800,
                            health=750, moveClass="vehicle", speed=4, radius=2.2, sightRange=24,
                            buildPower=100, buildRange=12, buildOptions=BUILDER_OPTIONS["vehicle"],
                            description="Armoured constructor with more build power."),
    "tank": dict(role="tank", tier=1, metalCost=120, energyCost=1300, buildTime=1300,
                 health=720, moveClass="vehicle", speed=4.8, radius=2.2, sightRange=26,
                 weapons=[cannon(19, 58, 1.4)],
                 description="Main battle unit. The backbone of any army."),
    "flak_vehicle": dict(role="flak_vehicle", tier=1, metalCost=130, energyCost=1500, buildTime=1400,
                         health=520, moveClass="vehicle", speed=4.4, radius=2.2, sightRange=28,
                         weapons=[flak(30, 26, 0.5)],
                         description="Shreds aircraft. Useless against ground."),
    "artillery_vehicle": dict(role="artillery_vehicle", tier=1, metalCost=220, energyCost=2400, buildTime=2400,
                              health=430, moveClass="vehicle", speed=3.2, radius=2.4, sightRange=24,
                              weapons=[artillery(56, 115, 4.0, 5, min_range=12)],
                              description="Long-range howitzer. Keep it behind the line."),
    "salvager": dict(role="salvager", tier=1, metalCost=90, energyCost=900, buildTime=900,
                     health=480, moveClass="vehicle", speed=6, radius=2, sightRange=24,
                     buildPower=40, buildRange=12, reclaimMultiplier=4, buildOptions=[],
                     description="Strips wrecks four times faster than a builder. Can't build."),

    # ---- Aircraft Plant ----------------------------------------------------------------------
    "builder_air": dict(role="builder_air", tier=1, metalCost=120, energyCost=2800, buildTime=2200,
                        health=230, moveClass="air", speed=11, radius=2, sightRange=28,
                        buildPower=60, buildRange=12, buildOptions=BUILDER_OPTIONS["air"],
                        description="Flying constructor. Builds anywhere, dies to anything."),
    "fighter": dict(role="fighter", tier=1, metalCost=90, energyCost=2600, buildTime=1500,
                    health=190, moveClass="air", speed=15, radius=1.8, sightRange=34,
                    weapons=[missile_aa(22, 48, 1.0, speed=120)],
                    description="Air superiority. Only shoots aircraft."),
    "bomber": dict(role="bomber", tier=1, metalCost=160, energyCost=4800, buildTime=2400,
                   health=640, moveClass="air", speed=10, radius=2.6, sightRange=28,
                   weapons=[bomb(170, 3.0, 6)],
                   description="Flies over its target and drops a heavy payload."),
    "gunship": dict(role="gunship", tier=1, metalCost=260, energyCost=5500, buildTime=3200,
                    health=950, moveClass="air", speed=7.5, radius=2.4, sightRange=30,
                    weapons=[laser(16, 23, 0.35, 0.85)],
                    description="Hovers and strafes ground targets."),

    # ---- Shipyard ----------------------------------------------------------------------------
    "builder_sea": dict(role="builder_sea", tier=1, metalCost=180, energyCost=1600, buildTime=1800,
                        health=950, moveClass="ship", speed=4.5, radius=3, sightRange=26,
                        buildPower=100, buildRange=14, buildOptions=BUILDER_OPTIONS["sea"],
                        description="Naval constructor."),
    "patrol_boat": dict(role="patrol_boat", tier=1, metalCost=110, energyCost=1100, buildTime=1300,
                        health=620, moveClass="ship", speed=7.5, radius=2.6, sightRange=30,
                        weapons=[cannon(20, 42, 1.0), missile_aa(22, 30, 1.6)],
                        description="Fast boat with a light gun and anti-air."),
    "destroyer": dict(role="destroyer", tier=1, metalCost=340, energyCost=4000, buildTime=4000,
                      health=2600, moveClass="ship", speed=4.8, radius=4, sightRange=32,
                      weapons=[cannon(30, 125, 2.0, splash=2.5)],
                      description="Heavy hull and a big gun."),
    "battleship": dict(role="battleship", tier=2, metalCost=2000, energyCost=18000, buildTime=18000,
                       health=9500, moveClass="ship", speed=3, radius=6, sightRange=34,
                       weapons=[artillery(75, 230, 3.2, 5, acc=0.75, min_range=14), laser(18, 30, 0.5)],
                       description="Floating fortress with long-range guns."),

    # ---- Advanced Assembly -------------------------------------------------------------------
    "builder_adv": dict(role="builder_adv", tier=2, metalCost=400, energyCost=5000, buildTime=6000,
                        health=950, moveClass="vehicle", speed=3.8, radius=2.6, sightRange=26,
                        buildPower=240, buildRange=14, buildOptions=ADV_BUILDER_OPTIONS["vehicle"],
                        description="Industry engineer: fusion reactors, deep extractors and the gantry."),
    "heavy_assault": dict(role="heavy_assault", tier=2, metalCost=480, energyCost=6000, buildTime=6000,
                          health=3300, moveClass="vehicle", speed=3.4, radius=3, sightRange=28,
                          weapons=[cannon(23, 160, 1.6, splash=2.2), laser(16, 30, 0.5)],
                          description="Siege tank. Soaks damage and hits hard."),
    "long_artillery": dict(role="long_artillery", tier=2, metalCost=700, energyCost=9000, buildTime=9000,
                           health=1250, moveClass="vehicle", speed=2.6, radius=3.2, sightRange=26,
                           weapons=[artillery(95, 270, 6.0, 7, acc=0.6, min_range=20)],
                           description="Shells bases from across the map. Needs radar or spotters."),
    "missile_tank": dict(role="missile_tank", tier=2, metalCost=380, energyCost=4600, buildTime=4600,
                         health=1300, moveClass="vehicle", speed=4.0, radius=2.8, sightRange=28,
                         weapons=[dict(kind="missile", range=44, damage=95, reloadSeconds=2.6, accuracy=0.92,
                                       projectileSpeed=75, targetsAir=True, targetsGround=True)],
                         description="Rocket launcher that hits ground and air alike."),

    # ---- Advanced bot lab (refit of the Bot Lab) -----------------------------------------------
    "builder_adv_bot": dict(role="builder_adv_bot", tier=2, metalCost=360, energyCost=4500, buildTime=5500,
                            health=820, moveClass="bot", speed=3.6, radius=2.2, sightRange=26,
                            buildPower=200, buildRange=12, buildOptions=ADV_BUILDER_OPTIONS["bot"],
                            description="Fortification engineer: heavy turrets and plasma batteries. Climbs hills."),
    "heavy_bot": dict(role="heavy_bot", tier=2, metalCost=420, energyCost=5200, buildTime=5200,
                      health=2600, moveClass="bot", speed=3.2, radius=2.8, sightRange=26,
                      weapons=[laser(20, 55, 0.8, 0.9), cannon(20, 90, 1.8, splash=1.8)],
                      description="Armoured walker that takes ground tanks can't climb."),
    "sniper_bot": dict(role="sniper_bot", tier=2, metalCost=300, energyCost=6000, buildTime=5000,
                       health=520, moveClass="bot", speed=3.4, radius=2.0, sightRange=40,
                       weapons=[laser(48, 520, 5.0, 0.98)],
                       description="One slow, devastating long-range shot. Keep it behind the line."),
    "amphibious": dict(role="amphibious", tier=2, metalCost=250, energyCost=3500, buildTime=3500,
                       health=1550, moveClass="amphibious", speed=4.2, radius=2.6, sightRange=26,
                       weapons=[laser(18, 36, 0.7)],
                       description="Walks under water and climbs out on the far shore."),
    # ---- Advanced aircraft plant (refit of the aircraft plant) ----------------------------------
    "builder_adv_air": dict(role="builder_adv_air", tier=2, metalCost=260, energyCost=6500, buildTime=4800,
                            health=600, moveClass="air", speed=9, radius=2.4, sightRange=30,
                            buildPower=90, buildRange=12, buildOptions=ADV_BUILDER_OPTIONS["air"],
                            description="Flying engineer. Builds any tier 2 structure, slowly."),
    "interceptor": dict(role="interceptor", tier=2, metalCost=140, energyCost=5200, buildTime=2600,
                        health=520, moveClass="air", speed=17, radius=2.0, sightRange=36,
                        weapons=[missile_aa(34, 70, 1.1, speed=140)],
                        description="Fast, tough air superiority. Only shoots aircraft."),
    "heavy_bomber": dict(role="heavy_bomber", tier=2, metalCost=420, energyCost=12000, buildTime=6000,
                         health=1500, moveClass="air", speed=6.5, radius=3.6, sightRange=26,
                         weapons=[bomb(420, 6.0, 9)],
                         description="Carpets a wide area. Slow; needs fighter cover."),

    # ---- Advanced shipyard (refit of the shipyard) --------------------------------------------
    "builder_adv_sea": dict(role="builder_adv_sea", tier=2, metalCost=420, energyCost=4200, buildTime=5000,
                            health=1500, moveClass="ship", speed=3.8, radius=3, sightRange=28,
                            buildPower=220, buildRange=16, buildOptions=ADV_BUILDER_OPTIONS["sea"],
                            description="Naval engineer: fusion, deep extractors and coastal heavy turrets."),
    "cruiser": dict(role="cruiser", tier=2, metalCost=900, energyCost=9000, buildTime=9000,
                    health=4200, moveClass="ship", speed=3.8, radius=4.4, sightRange=34,
                    weapons=[cannon(40, 180, 1.6, splash=2), flak(34, 40, 0.6)],
                    description="Escort with a heavy gun and flak. Guards the fleet from the air."),
    "missile_ship": dict(role="missile_ship", tier=2, metalCost=1100, energyCost=12000, buildTime=11000,
                         health=2400, moveClass="ship", speed=3.2, radius=4.4, sightRange=30,
                         weapons=[dict(kind="missile", range=120, damage=380, reloadSeconds=6.0, accuracy=0.9,
                                       projectileSpeed=60, targetsAir=False, targetsGround=True, splash=4)],
                         description="Long-range cruise missiles. Strikes deep inland."),

    "experimental": dict(role="experimental", tier=3, metalCost=7500, energyCost=110000, buildTime=90000,
                         health=32000, moveClass="bot", speed=2.2, radius=8, sightRange=44,
                         weapons=[], deathExplosionDamage=4000, deathExplosionRadius=36, wreckFraction=0.5,
                         description=""),

    # ---- Economy structures ------------------------------------------------------------------
    "metal_extractor": dict(role="metal_extractor", tier=1, metalCost=50, energyCost=520, buildTime=1000,
                            health=600, footprint=1, needsMetalSpot=True, extractorMultiplier=1, sightRange=14,
                            description="Pulls metal from a metal spot."),
    "solar_collector": dict(role="solar_collector", tier=1, metalCost=150, energyCost=0, buildTime=2000,
                            health=800, footprint=2, energyIncome=20, energyStorage=50, sightRange=14,
                            description="Steady 20 energy a second."),
    "wind_turbine": dict(role="wind_turbine", tier=1, metalCost=40, energyCost=180, buildTime=1200,
                         health=320, footprint=1, usesWind=True, sightRange=12,
                         description="Cheap energy that rises and falls with the map's wind."),
    "metal_storage": dict(role="metal_storage", tier=1, metalCost=200, energyCost=600, buildTime=2000,
                          health=1600, footprint=2, metalStorage=3000, sightRange=12,
                          description="Holds 3,000 more metal."),
    "energy_storage": dict(role="energy_storage", tier=1, metalCost=170, energyCost=1700, buildTime=2500,
                           health=1600, footprint=2, energyStorage=6000, sightRange=12,
                           deathExplosionDamage=400, deathExplosionRadius=12,
                           description="Holds 6,000 more energy. Explodes when destroyed."),
    "metal_maker": dict(role="metal_maker", tier=1, metalCost=1, energyCost=2400, buildTime=2600,
                        health=420, footprint=2, convertEnergy=60, convertMetal=1, sightRange=12,
                        description="Turns 60 energy into 1 metal a second while energy is above 10%."),

    # ---- Factories ---------------------------------------------------------------------------
    "bot_lab": dict(role="factory_bot", tier=1, metalCost=500, energyCost=1000, buildTime=5000,
                    health=2600, footprint=3, buildPower=100, sightRange=22,
                    buildOptions=FACTORY_OPTIONS["bot_lab"], description="Builds walking units."),
    "vehicle_plant": dict(role="factory_vehicle", tier=1, metalCost=600, energyCost=1200, buildTime=6000,
                          health=3100, footprint=3, buildPower=100, sightRange=22,
                          buildOptions=FACTORY_OPTIONS["vehicle_plant"], description="Builds tanks and vehicles."),
    "aircraft_plant": dict(role="factory_air", tier=1, metalCost=800, energyCost=1600, buildTime=7500,
                           health=2100, footprint=3, buildPower=100, sightRange=22,
                           buildOptions=FACTORY_OPTIONS["aircraft_plant"], description="Builds aircraft."),
    "shipyard": dict(role="factory_ship", tier=1, metalCost=600, energyCost=1200, buildTime=6000,
                     health=3100, footprint=3, buildPower=100, needsWater=True, sightRange=22,
                     buildOptions=FACTORY_OPTIONS["shipyard"], description="Builds ships. Must sit in deep water."),

    # ---- Defence and intel -------------------------------------------------------------------
    "light_turret": dict(role="light_turret", tier=1, metalCost=95, energyCost=1000, buildTime=1500,
                         health=1250, footprint=1, sightRange=30,
                         weapons=[laser(28, 32, 0.6, 0.9)], description="Cheap laser tower."),
    "aa_turret": dict(role="aa_turret", tier=1, metalCost=150, energyCost=1400, buildTime=2000,
                      health=850, footprint=1, sightRange=34,
                      weapons=[missile_aa(42, 62, 1.4)], description="Anti-air missile battery."),
    "radar_tower": dict(role="radar_tower", tier=1, metalCost=50, energyCost=500, buildTime=800,
                        health=320, footprint=1, sightRange=22, radarRange=140,
                        description="Shows enemy movement as radar blips. Lets artillery fire at range."),

    # ---- Tier 2 structures -------------------------------------------------------------------
    "deep_extractor": dict(role="deep_extractor", tier=2, metalCost=600, energyCost=7000, buildTime=10000,
                           health=2600, footprint=1, needsMetalSpot=True, extractorMultiplier=3, sightRange=14,
                           description="Three times the metal of a standard extractor. Built over your old one."),
    "fusion_reactor": dict(role="fusion_reactor", tier=2, metalCost=4000, energyCost=30000, buildTime=70000,
                           health=5200, footprint=3, energyIncome=1000, energyStorage=2500, sightRange=16,
                           deathExplosionDamage=3500, deathExplosionRadius=34,
                           description="1,000 energy a second. A huge blast if it dies."),
    "heavy_turret": dict(role="heavy_turret", tier=2, metalCost=900, energyCost=12000, buildTime=15000,
                         health=6200, footprint=2, sightRange=40,
                         weapons=[cannon(42, 320, 2.2, splash=3, speed=90)], description="Heavy cannon emplacement."),
    "plasma_battery": dict(role="plasma_battery", tier=2, metalCost=3000, energyCost=40000, buildTime=50000,
                           health=4200, footprint=2, sightRange=30,
                           weapons=[artillery(220, 480, 6.5, 8, acc=0.55, min_range=40, speed=60)],
                           description="Strategic artillery. Covers a third of the map."),
    # Advanced factories are refits: built over your own tier 1 factory of the same kind, which
    # goes offline while the refit is under way. They keep the tier 1 list and add tier 2.
    "adv_bot_lab": dict(role="factory_adv_bot", tier=2, metalCost=2400, energyCost=8000, buildTime=18000,
                        health=4600, footprint=3, buildPower=220, sightRange=22, upgradesFrom="factory_bot",
                        buildOptions=FACTORY_OPTIONS["adv_bot_lab"],
                        description="Refit of a Bot Lab. Adds tier 2 walkers and the fortification engineer."),
    "advanced_assembly": dict(role="factory_adv_vehicle", tier=2, metalCost=2800, energyCost=9000, buildTime=20000,
                              health=5200, footprint=3, buildPower=240, sightRange=22, upgradesFrom="factory_vehicle",
                              buildOptions=FACTORY_OPTIONS["advanced_assembly"],
                              description="Refit of a Vehicle Plant. Adds tier 2 vehicles and the industry engineer."),
    "adv_aircraft_plant": dict(role="factory_adv_air", tier=2, metalCost=3000, energyCost=11000, buildTime=22000,
                               health=4000, footprint=3, buildPower=220, sightRange=22, upgradesFrom="factory_air",
                               buildOptions=FACTORY_OPTIONS["adv_aircraft_plant"],
                               description="Refit of an aircraft plant. Adds interceptors, heavy bombers and the flying engineer."),
    "adv_shipyard": dict(role="factory_adv_ship", tier=2, metalCost=3000, energyCost=10000, buildTime=22000,
                         health=5600, footprint=3, buildPower=240, needsWater=True, sightRange=22,
                         upgradesFrom="factory_ship", buildOptions=FACTORY_OPTIONS["adv_shipyard"],
                         description="Refit of a shipyard. Adds cruisers, missile ships, battleships and the naval engineer."),
    "experimental_gantry": dict(role="factory_gantry", tier=2, metalCost=6000, energyCost=30000, buildTime=40000,
                                health=8500, footprint=4, buildPower=600, sightRange=24,
                                buildOptions=FACTORY_OPTIONS["experimental_gantry"],
                                description="Builds your faction's experimental war machine."),
}

EXPERIMENTALS = {
    "lattice": dict(
        description="Skywarden: a towering walker whose beam cuts a lane across the battlefield.",
        weapons=[dict(kind="laser", range=60, damage=420, reloadSeconds=1.8, accuracy=0.95),
                 missile_aa(36, 90, 1.0)],
        moveClass="bot", speed=2.4),
    "crucible": dict(
        description="Mantle Crawler: a tracked smelter that eats wrecks to repair itself and fires molten slag.",
        weapons=[artillery(64, 520, 3.4, 9, acc=0.8, min_range=10, speed=50),
                 dict(kind="laser", range=15, damage=90, reloadSeconds=0.5, accuracy=0.9)],
        moveClass="vehicle", speed=1.9, selfReclaimRange=26),
}


# Everything costs this share of its listed energy. Tuned so a factory needs about three solars
# rather than six, which keeps the early game moving.
ENERGY_SCALE = 0.55


# Salvage: higher-tier designs take more wrecks to reverse-engineer.
SALVAGE_WRECKS = {1: 3, 2: 4, 3: 6}


def unit_id(faction, key):
    return ID_OVERRIDES.get((faction, key), f"{faction}_{key}")


def scaled(value, factor):
    return round(value * factor, 2)


def build_unit(faction, key, base):
    mod = FACTIONS[faction]
    u = json.loads(json.dumps(base))  # deep copy
    if key == "experimental":
        u.update(json.loads(json.dumps(EXPERIMENTALS[faction])))
    lattice_name, crucible_name = NAMES[key]
    out = dict(id=unit_id(faction, key), name=lattice_name if faction == "lattice" else crucible_name,
               faction=faction)
    out.update(u)
    out["health"] = round(out["health"] * mod["hp"])
    if key != "commander":
        out["energyCost"] = int(round(out["energyCost"] * ENERGY_SCALE / 10) * 10)
    if out.get("speed"):
        out["speed"] = scaled(out["speed"], mod["speed"])
    if out.get("sightRange"):
        out["sightRange"] = scaled(out["sightRange"], mod["sight"])
    if out.get("reclaimMultiplier") or out.get("buildPower"):
        out["reclaimMultiplier"] = scaled(out.get("reclaimMultiplier", 1), mod["reclaim"])
    for w in out.get("weapons", []):
        if w["kind"] != "dgun":
            w["range"] = scaled(w["range"], mod["range"])
    if "buildOptions" in out:
        out["buildOptions"] = [unit_id(faction, k) for k in out["buildOptions"]]
    if out.get("moveClass") not in (None, "air") and key != "commander":  # aircraft leave no wrecks
        # Enemy wrecks of this design to reclaim before your factories can copy it.
        out["salvageWrecks"] = SALVAGE_WRECKS[out.get("tier", 1)]
    return out


def main():
    for faction in FACTIONS:
        folder = os.path.join(ROOT, faction)
        if os.path.isdir(folder):
            shutil.rmtree(folder)
        os.makedirs(folder)
        for key, base in UNITS.items():
            unit = build_unit(faction, key, base)
            with open(os.path.join(folder, f"{unit['id'].split('_', 1)[1]}.json"), "w") as f:
                json.dump(unit, f, indent=2)
                f.write("\n")
    print(f"Wrote {len(UNITS) * len(FACTIONS)} unit files")


if __name__ == "__main__":
    main()
