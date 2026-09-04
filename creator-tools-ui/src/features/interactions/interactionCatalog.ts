export const interactionItems = [
  {
    id: "hilda_green_zeppelin",
    titleKey: "interactions.zeppelin.green.title",
    imageAltKey: "interactions.zeppelin.green.imageAlt",
    typeKey: "interactions.zeppelin.type",
    image: "/assets/creator-tools/interactions/green-zeppelin.png",
  },
  {
    id: "hilda_purple_zeppelin",
    titleKey: "interactions.zeppelin.purple.title",
    imageAltKey: "interactions.zeppelin.purple.imageAlt",
    typeKey: "interactions.zeppelin.type",
    image: "/assets/creator-tools/interactions/purple-zeppelin.png",
  },
  {
    id: "rootpack_homing_carrot",
    titleKey: "interactions.rootpack.homingCarrot.title",
    imageAltKey: "interactions.rootpack.homingCarrot.imageAlt",
    typeKey: "interactions.rootpack.type",
    image: "/assets/creator-tools/interactions/homing-carrot.png",
  },
  {
    id: "cagney_homing_plant",
    titleKey: "interactions.cagney.homingPlant.title",
    imageAltKey: "interactions.cagney.homingPlant.imageAlt",
    typeKey: "interactions.cagney.type",
    image: "/assets/creator-tools/interactions/cagney-homing-plant.png",
  },
  {
    id: "frogs_firefly",
    titleKey: "interactions.frogs.firefly.title",
    imageAltKey: "interactions.frogs.firefly.imageAlt",
    typeKey: "interactions.frogs.type",
    image: "/assets/creator-tools/interactions/frogs-firefly.png",
  },
  {
    id: "robot_homing_bomb",
    titleKey: "interactions.robot.homingBomb.title",
    imageAltKey: "interactions.robot.homingBomb.imageAlt",
    typeKey: "interactions.robot.type",
    image: "/assets/creator-tools/interactions/robot-homing-bomb.png",
  },
] as const;

export function interactionItemFor(item: string) {
  return interactionItems.find((catalogItem) => catalogItem.id === item);
}
