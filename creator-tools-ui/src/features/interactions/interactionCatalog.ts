export const interactionCategories = ["attack", "mini_boss"] as const;
export type InteractionCategory = typeof interactionCategories[number];
export type InteractionCategoryFilter = "all" | InteractionCategory;

export const interactionItems = [
  {
    id: "hilda_green_zeppelin",
    category: "attack",
    titleKey: "interactions.zeppelin.green.title",
    imageAltKey: "interactions.zeppelin.green.imageAlt",
    typeKey: "interactions.zeppelin.type",
    image: "/assets/creator-tools/interactions/green-zeppelin.png",
  },
  {
    id: "hilda_purple_zeppelin",
    category: "attack",
    titleKey: "interactions.zeppelin.purple.title",
    imageAltKey: "interactions.zeppelin.purple.imageAlt",
    typeKey: "interactions.zeppelin.type",
    image: "/assets/creator-tools/interactions/purple-zeppelin.png",
  },
  {
    id: "rootpack_homing_carrot",
    category: "attack",
    titleKey: "interactions.rootpack.homingCarrot.title",
    imageAltKey: "interactions.rootpack.homingCarrot.imageAlt",
    typeKey: "interactions.rootpack.type",
    image: "/assets/creator-tools/interactions/homing-carrot.png",
  },
  {
    id: "cagney_homing_plant",
    category: "attack",
    titleKey: "interactions.cagney.homingPlant.title",
    imageAltKey: "interactions.cagney.homingPlant.imageAlt",
    typeKey: "interactions.cagney.type",
    image: "/assets/creator-tools/interactions/cagney-homing-plant.png",
  },
  {
    id: "frogs_firefly",
    category: "attack",
    titleKey: "interactions.frogs.firefly.title",
    imageAltKey: "interactions.frogs.firefly.imageAlt",
    typeKey: "interactions.frogs.type",
    image: "/assets/creator-tools/interactions/frogs-firefly.png",
  },
  {
    id: "robot_homing_bomb",
    category: "attack",
    titleKey: "interactions.robot.homingBomb.title",
    imageAltKey: "interactions.robot.homingBomb.imageAlt",
    typeKey: "interactions.robot.type",
    image: "/assets/creator-tools/interactions/robot-homing-bomb.png",
  },
  {
    id: "baroness_head_toss",
    category: "attack",
    titleKey: "interactions.baroness.headToss.title",
    imageAltKey: "interactions.baroness.headToss.imageAlt",
    typeKey: "interactions.baroness.type",
    image: "/assets/creator-tools/interactions/baroness-head-toss.png",
  },
  {
    id: "dragon_fireballs",
    category: "attack",
    titleKey: "interactions.dragon.fireballs.title",
    imageAltKey: "interactions.dragon.fireballs.imageAlt",
    typeKey: "interactions.dragon.type",
    image: "/assets/creator-tools/interactions/dragon-fireballs.png",
  },
  {
    id: "baroness_cupcake",
    category: "mini_boss",
    titleKey: "interactions.baroness.cupcake.title",
    imageAltKey: "interactions.baroness.cupcake.imageAlt",
    typeKey: "interactions.miniBoss.type",
    image: "/assets/creator-tools/interactions/baroness-cupcake.png",
  },
  {
    id: "baroness_gumball",
    category: "mini_boss",
    titleKey: "interactions.baroness.gumball.title",
    imageAltKey: "interactions.baroness.gumball.imageAlt",
    typeKey: "interactions.miniBoss.type",
    image: "/assets/creator-tools/interactions/baroness-gumball.png",
  },
  {
    id: "baroness_waffle",
    category: "mini_boss",
    titleKey: "interactions.baroness.waffle.title",
    imageAltKey: "interactions.baroness.waffle.imageAlt",
    typeKey: "interactions.miniBoss.type",
    image: "/assets/creator-tools/interactions/baroness-waffle.png",
  },
  {
    id: "baroness_candy_corn",
    category: "mini_boss",
    titleKey: "interactions.baroness.candyCorn.title",
    imageAltKey: "interactions.baroness.candyCorn.imageAlt",
    typeKey: "interactions.miniBoss.type",
    image: "/assets/creator-tools/interactions/baroness-candy-corn.png",
  },
  {
    id: "baroness_jawbreaker",
    category: "mini_boss",
    titleKey: "interactions.baroness.jawbreaker.title",
    imageAltKey: "interactions.baroness.jawbreaker.imageAlt",
    typeKey: "interactions.miniBoss.type",
    image: "/assets/creator-tools/interactions/baroness-jawbreaker.png",
  },
] as const;

export function interactionItemFor(item: string) {
  return interactionItems.find((catalogItem) => catalogItem.id === item);
}
