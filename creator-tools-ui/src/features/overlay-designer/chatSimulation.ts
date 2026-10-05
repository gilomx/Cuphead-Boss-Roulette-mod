import type { ChatChoiceOption, ChatChoosesStage, ChatChoosesState } from "../../model";

type ChoiceSeed = [string, string, string];
const POOLS: Record<Exclude<ChatChoosesStage, "result">, ChoiceSeed[]> = {
  boss: [
    ["Hosco y Tosco", "Ribby and Croaks", "bosses/hoscoytosco.png"],
    ["La pandilla raíz", "The Root Pack", "bosses/pandillaraiz.png"],
    ["Goopy Le Grande", "Goopy Le Grande", "bosses/goopylegrande.png"],
    ["Hilda Berg", "Hilda Berg", "bosses/hilda.png"],
    ["Clavel de Cagney", "Cagney Carnation", "bosses/claveldecagney.png"],
    ["Baronesa Von Bon Bon", "Baroness Von Bon Bon", "bosses/baronesa.png"],
  ],
  weapon1: [
    ["Lanzaguisantes", "Peashooter", "weapons/lanzaguisantes.png"],
    ["Expansión", "Spread", "weapons/expansion.png"],
    ["Rastreador", "Chaser", "weapons/rastreador.png"],
    ["Globero", "Lobber", "weapons/globero.png"],
    ["Carga", "Charge", "weapons/carga.png"],
    ["Rodeo", "Roundabout", "weapons/rodeo.png"],
  ],
  weapon2: [
    ["Expansión", "Spread", "weapons/expansion.png"],
    ["Rastreador", "Chaser", "weapons/rastreador.png"],
    ["Globero", "Lobber", "weapons/globero.png"],
    ["Carga", "Charge", "weapons/carga.png"],
    ["Rodeo", "Roundabout", "weapons/rodeo.png"],
    ["Nada", "None", "creator-tools/empty.png"],
  ],
  super: [
    ["Super I", "Super I", "supers/super1.png"],
    ["Super II", "Super II", "supers/super2.png"],
    ["Super III", "Super III", "supers/super3.png"],
    ["Nada", "None", "creator-tools/empty.png"],
  ],
  charm: [
    ["Corazón", "Heart", "charms/corazon.png"],
    ["Café", "Coffee", "charms/cafe.png"],
    ["Bomba de humo", "Smoke Bomb", "charms/bombadehumo.png"],
    ["Desvío dulce", "P. Sugar", "charms/desviodulce.png"],
    ["Corazón doble", "Twin Heart", "charms/corazondoble.png"],
    ["Nada", "None", "creator-tools/empty.png"],
  ],
  modifier: [
    ["No Dash", "No Dash", "creator-tools/modifiers/nodash_01.png"],
    ["No EX", "No EX", "creator-tools/modifiers/noex_01.png"],
    ["RGB", "RGB", "creator-tools/modifiers/rgb_01.png"],
    ["HP. 1", "HP. 1", "creator-tools/modifiers/hp1_01.png"],
    ["Lluvia de tinta", "Ink Rain", "creator-tools/modifiers/inkrain_01.png"],
    ["Nada", "None", "creator-tools/empty.png"],
  ],
};
export type ChatSimulationAction =
  | { type: "scenario"; phase: ChatChoosesState["phase"] }
  | { type: "stage"; stage: Exclude<ChatChoosesStage, "result"> }
  | { type: "count"; count: number }
  | { type: "vote"; number: number }
  | { type: "locale"; locale: string }
  | { type: "reset" };
type ChatSimulationState = ChatChoosesState & { previewLocale: string };

function choices(stage: Exclude<ChatChoosesStage, "result">, locale: string): ChatChoiceOption[] {
  return POOLS[stage].map(([es, en, image], index) => ({
    id: index + 1, number: index + 1, name: locale === "en" ? en : es, image,
    none: image.endsWith("empty.png"), votes: [8, 14, 6, 4, 10, 3][index],
  }));
}

export function createChatSimulation(locale = "es"): ChatSimulationState {
  const options = choices("boss", locale);
  return {
    previewLocale: locale, ready: true, revision: 1, sessionId: 1, round: 1, phase: "voting",
    stage: "boss", nextStage: "weapon1", withChallenge: true, plane: false,
    mapAvailable: true, remainingSeconds: 0, totalVotes: 45, winnerNumber: 2,
    outcome: "most_votes", blockedByLiveEvent: "", feedback: "ready", error: false,
    options,
    selected: Object.fromEntries(Object.keys(POOLS).map((key) => {
      const stage = key as Exclude<ChatChoosesStage, "result">;
      return [stage, choices(stage, locale)[stage === "boss" ? 1 : 0]];
    })),
  };
}

export function chatSimulationReducer(state: ChatSimulationState, action: ChatSimulationAction): ChatSimulationState {
  if (action.type === "reset") return { ...createChatSimulation(state.previewLocale), revision: state.revision + 1, sessionId: state.sessionId + 1 };
  if (action.type === "locale") {
    const localized = createChatSimulation(action.locale);
    const stage = state.stage === "result" ? "boss" : state.stage;
    return { ...state, previewLocale: action.locale, revision: state.revision + 1, selected: localized.selected,
      options: choices(stage, action.locale).slice(0, state.options.length)
        .map((item, index) => ({ ...item, votes: state.options[index]?.votes ?? 0 })) };
  }
  let next = { ...state, revision: state.revision + 1 };
  if (action.type === "scenario") next.phase = action.phase;
  if (action.type === "stage") {
    next = { ...next, phase: "voting", stage: action.stage, round: state.round + 1,
      options: choices(action.stage, state.previewLocale) };
  }
  if (action.type === "count") {
    const stage = state.stage === "result" ? "boss" : state.stage;
    next.options = choices(stage, state.previewLocale).slice(0, stage === "super" ? 4 : Math.max(2, Math.min(6, action.count)));
    next.round++;
  }
  if (action.type === "vote" && state.phase === "voting") {
    next.options = state.options.map(option => option.number === action.number
      ? { ...option, votes: (option.votes ?? 0) + 1 } : option);
  }
  next.totalVotes = next.options.reduce((total, option) => total + (option.votes ?? 0), 0);
  next.winnerNumber = next.options.reduce((winner, option) =>
    (option.votes ?? 0) > (winner.votes ?? 0) ? option : winner, next.options[0])?.number ?? 0;
  return next;
}
