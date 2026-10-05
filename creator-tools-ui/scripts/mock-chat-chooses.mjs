// Local UI fixture. Production voting is owned by CreatorToolsChatChoosesController.
export function createChatChoosesMock() {
  const pools = {
    boss: [['Hosco y Tosco', 'bosses/hoscoytosco.png'], ['La pandilla raíz', 'bosses/pandillaraiz.png'], ['Goopy Le Grande', 'bosses/goopylegrande.png'], ['Hilda Berg', 'bosses/hilda.png', 'plane'], ['Clavel de Cagney', 'bosses/claveldecagney.png'], ['Baronesa Von Bon Bon', 'bosses/baronesa.png']],
    weapon1: [['Lanzaguisantes', 'weapons/lanzaguisantes.png'], ['Expansión', 'weapons/expansion.png'], ['Rastreador', 'weapons/rastreador.png'], ['Globero', 'weapons/globero.png'], ['Carga', 'weapons/carga.png'], ['Rodeo', 'weapons/rodeo.png'], ['Tiro certero', 'weapons/tirocertero.png'], ['Nada', 'creator-tools/empty.png', 'none']],
    super: [['Super I', 'supers/super1.png'], ['Super II', 'supers/super2.png'], ['Super III', 'supers/super3.png'], ['Nada', 'creator-tools/empty.png', 'none']],
    charm: [['Corazón', 'charms/corazon.png'], ['Café', 'charms/cafe.png'], ['Bomba de humo', 'charms/bombadehumo.png'], ['Desvío dulce', 'charms/desviodulce.png'], ['Corazón doble', 'charms/corazondoble.png'], ['Galletita Astral', 'charms/galletitaastral.png'], ['Nada', 'creator-tools/empty.png', 'none']],
    modifier: [['No Dash', 'modifiers/nodash_01.png', 'ground'], ['No miniavión', 'modifiers/nomini_01.png', 'plane'], ['No EX', 'modifiers/noex_01.png'], ['RGB', 'modifiers/rgb_01.png'], ['HP. 1', 'modifiers/hp1_01.png'], ['Lluvia de tinta', 'modifiers/inkrain_01.png'], ['Nada', 'creator-tools/empty.png', 'none']],
  };
  pools.weapon2 = pools.weapon1;
  const keys = ['boss', 'weapon1', 'weapon2', 'super', 'charm', 'modifier'];
  const votes = new Map();
  let deadline = 0;
  const state = { ready: true, revision: 0, sessionId: 0, round: 0, phase: 'off', stage: 'boss', nextStage: 'weapon1', withChallenge: true,
    plane: false, mapAvailable: true, remainingSeconds: 0, totalVotes: 0, winnerNumber: 0, outcome: '', blockedByLiveEvent: '', feedback: 'ready', error: false, options: [], selected: {} };
  const next = () => state.stage === 'boss' && state.plane ? 'charm' :
    state.stage === 'charm' && !state.withChallenge ? 'result' : keys[keys.indexOf(state.stage) + 1] ?? 'result';
  const open = () => {
    let pool = pools[state.stage].map(([name, image, flag], id) => ({ id, name, image, none: flag === 'none', flag }));
    if (state.stage === 'weapon1') pool = pool.filter(x => !x.none);
    if (state.stage === 'weapon2') pool = pool.filter(x => x.id !== state.selected.weapon1.id);
    if (state.stage === 'modifier') pool = pool.filter(x => !['plane', 'ground'].includes(x.flag) || x.flag === (state.plane ? 'plane' : 'ground'));
    state.options = pool.sort(() => Math.random() - .5).slice(0, 6).map((x, i) => ({ ...x, number: i + 1, votes: 0 }));
    state.round++; state.totalVotes = 0; votes.clear(); state.winnerNumber = 0; state.outcome = ''; state.phase = 'voting'; state.nextStage = next(); state.revision++;
  };
  function snapshot() {
    if (deadline && Date.now() >= deadline) {
      deadline = 0;
      if (state.phase === 'reveal') {
        state.stage = next();
        if (state.stage === 'result') { state.phase = 'result'; deadline = Date.now() + 5000; }
        else open();
      } else if (state.phase === 'result') { state.phase = 'countdown'; deadline = Date.now() + 3000; }
      else if (state.phase === 'countdown') state.phase = 'active';
      state.revision++;
    }
    state.remainingSeconds = Math.max(0, Math.ceil((deadline - Date.now()) / 1000));
    state.nextStage = next();
    return state;
  }
  function command(query, blocker) {
    snapshot(); state.error = false; state.feedback = 'ready';
    const operation = query.get('operation');
    const locked = !['off', 'completed'].includes(state.phase);
    if (['stop', 'finish'].includes(operation)) { state.phase = 'off'; state.options = []; state.selected = {}; deadline = 0; }
    else if ((operation === 'start' || operation === 'save') && !locked) {
      if (blocker && operation === 'start') { state.error = true; state.feedback = 'blocked_by_live_event'; }
      else {
        state.withChallenge = query.get('mode') === 'with';
        if (operation === 'start') { state.sessionId++; state.round = 0; state.selected = {}; state.plane = false; state.stage = 'boss'; open(); }
      }
    } else if (operation === 'next' && state.phase === 'voting' && Number(query.get('sessionId')) === state.sessionId && Number(query.get('round')) === state.round) {
      const max = Math.max(...state.options.map(o => o.votes));
      const tied = state.options.filter(o => o.votes === max);
      const winner = tied[Math.floor(Math.random() * tied.length)];
      state.winnerNumber = winner.number; state.selected[state.stage] = winner;
      state.outcome = max === 0 ? 'no_votes' : tied.length > 1 ? 'tie' : 'most_votes';
      if (state.stage === 'boss') state.plane = winner.flag === 'plane';
      state.phase = 'reveal'; deadline = Date.now() + 1500;
    } else { state.error = true; state.feedback = 'stale_round'; }
    state.revision++; return snapshot();
  }
  function vote(entry) {
    snapshot();
    const number = Number(entry.chatText);
    const option = state.options.find(o => o.number === number);
    if (state.phase !== 'voting' || !option) return '';
    const identity = `${entry.platform}:${entry.userId || entry.user}`;
    const old = votes.get(identity);
    if (old === number) return 'chat_vote_unchanged';
    if (old) state.options.find(o => o.number === old).votes--;
    votes.set(identity, number); option.votes++; state.totalVotes = votes.size; state.revision++;
    return 'chat_vote_counted';
  }
  return { snapshot, command, vote, active: () => !['off', 'completed'].includes(state.phase) };
}
