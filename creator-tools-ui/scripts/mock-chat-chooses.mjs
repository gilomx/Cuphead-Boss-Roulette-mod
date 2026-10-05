// Local UI fixture. Production voting is owned by CreatorToolsChatChoosesController.
export function createChatChoosesMock({ developmentTools = true, clock = Date.now, random = Math.random } = {}) {
  const pools = {
    boss: [['Hosco y Tosco', 'bosses/hoscoytosco.png'], ['La pandilla raíz', 'bosses/pandillaraiz.png'], ['Goopy Le Grande', 'bosses/goopylegrande.png'], ['Hilda Berg', 'bosses/hilda.png', 'plane'], ['Clavel de Cagney', 'bosses/claveldecagney.png'], ['Baronesa Von Bon Bon', 'bosses/baronesa.png']],
    weapon1: [['Lanzaguisantes', 'weapons/lanzaguisantes.png'], ['Expansión', 'weapons/expansion.png'], ['Rastreador', 'weapons/rastreador.png'], ['Globero', 'weapons/globero.png'], ['Carga', 'weapons/carga.png'], ['Rodeo', 'weapons/rodeo.png'], ['Tiro certero', 'weapons/tirocertero.png'], ['Nada', 'creator-tools/empty.png', 'none']],
    super: [['Super I', 'supers/super1.png'], ['Super II', 'supers/super2.png'], ['Super III', 'supers/super3.png'], ['Nada', 'creator-tools/empty.png', 'none']],
    charm: [['Corazón', 'charms/corazon.png'], ['Café', 'charms/cafe.png'], ['Bomba de humo', 'charms/bombadehumo.png'], ['Desvío dulce', 'charms/desviodulce.png'], ['Corazón doble', 'charms/corazondoble.png'], ['Galletita Astral', 'charms/galletitaastral.png'], ['Nada', 'creator-tools/empty.png', 'none']],
    modifier: [['No Dash', 'creator-tools/modifiers/nodash_01.png', 'ground'], ['No miniavión', 'creator-tools/modifiers/nomini_01.png', 'plane'], ['No EX', 'creator-tools/modifiers/noex_01.png'], ['RGB', 'creator-tools/modifiers/rgb_01.png'], ['HP. 1', 'creator-tools/modifiers/hp1_01.png'], ['Lluvia de tinta', 'creator-tools/modifiers/inkrain_01.png'], ['Nada', 'creator-tools/empty.png', 'none']],
  };
  pools.weapon2 = pools.weapon1;
  const keys = ['boss', 'weapon1', 'weapon2', 'super', 'charm', 'modifier'];
  const votes = new Map();
  let deadline = 0;
  let testPlan = [], testStarted = 0, generation = 0, run = 0;
  const state = { ready: true, developmentTools, testVotes: { active: false, sent: 0, total: 0, remainingSeconds: 0 }, revision: 0, sessionId: 0, round: 0, phase: 'off', stage: 'boss', nextStage: 'weapon1', withChallenge: true,
    plane: false, mapAvailable: true, remainingSeconds: 0, totalVotes: 0, winnerNumber: 0, outcome: '', blockedByLiveEvent: '', feedback: 'ready', error: false, options: [], selected: {} };
  const next = () => state.stage === 'boss' && state.plane ? 'charm' :
    state.stage === 'charm' && !state.withChallenge ? 'result' : keys[keys.indexOf(state.stage) + 1] ?? 'result';
  const open = () => {
    cancelTestVotes();
    let pool = pools[state.stage].map(([name, image, flag], id) => ({ id, name, image, none: flag === 'none', flag }));
    if (state.stage === 'weapon1') pool = pool.filter(x => !x.none);
    if (state.stage === 'weapon2') pool = pool.filter(x => x.id !== state.selected.weapon1.id);
    if (state.stage === 'modifier') pool = pool.filter(x => !['plane', 'ground'].includes(x.flag) || x.flag === (state.plane ? 'plane' : 'ground'));
    state.options = (state.stage === 'super' ? pool : pool.sort(() => random() - .5)).slice(0, 6).map((x, i) => ({ ...x, number: i + 1, votes: 0 }));
    state.round++; state.totalVotes = 0; votes.clear(); state.winnerNumber = 0; state.outcome = ''; state.phase = 'voting'; state.nextStage = next(); state.revision++;
  };
  function snapshot() {
    if (deadline && clock() >= deadline) {
      deadline = 0;
      if (state.phase === 'reveal') {
        state.stage = next();
        if (state.stage === 'result') state.phase = 'result';
        else open();
      }
      state.revision++;
    }
    state.remainingSeconds = Math.max(0, Math.ceil((deadline - clock()) / 1000));
    state.testVotes.remainingSeconds = state.testVotes.active ? Math.max(0, Math.ceil(10 - (clock() - testStarted) / 1000)) : 0;
    state.nextStage = next();
    return state;
  }
  function command(query, blocker) {
    snapshot(); state.error = false; state.feedback = 'ready';
    const operation = query.get('operation');
    const locked = !['off', 'completed'].includes(state.phase);
    if (operation === 'test_votes' && !developmentTools) { state.error = true; state.feedback = 'development_only'; }
    else if (['stop', 'finish'].includes(operation)) { cancelTestVotes(); state.phase = 'off'; state.options = []; state.selected = {}; deadline = 0; }
    else if ((operation === 'start' || operation === 'save') && !locked) {
      if (blocker && operation === 'start') { state.error = true; state.feedback = 'blocked_by_live_event'; }
      else {
        state.withChallenge = query.get('mode') === 'with';
        if (operation === 'start') { state.sessionId++; state.round = 0; state.selected = {}; state.plane = false; state.stage = 'boss'; open(); }
      }
    } else if (['next', 'test_votes'].includes(operation) && state.phase === 'voting' && Number(query.get('sessionId')) === state.sessionId && Number(query.get('round')) === state.round) {
      if (operation === 'test_votes') {
        if (state.testVotes.active) { state.error = true; state.feedback = 'test_votes_running'; }
        else startTestVotes();
        state.revision++; return snapshot();
      }
      cancelTestVotes();
      const max = Math.max(...state.options.map(o => o.votes));
      const tied = state.options.filter(o => o.votes === max);
      const winner = tied[Math.floor(random() * tied.length)];
      state.winnerNumber = winner.number; state.selected[state.stage] = winner;
      state.outcome = max === 0 ? 'no_votes' : tied.length > 1 ? 'tie' : 'most_votes';
      if (state.stage === 'boss') state.plane = winner.flag === 'plane';
      state.phase = 'reveal'; deadline = clock() + 1500;
    } else { state.error = true; state.feedback = 'stale_round'; }
    state.revision++; return snapshot();
  }
  function vote(entry) {
    snapshot();
    if (entry.testVoteGeneration && (entry.testVoteGeneration !== generation || entry.testVoteSession !== state.sessionId || entry.testVoteRound !== state.round)) return '';
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
  function cancelTestVotes() {
    testPlan = []; generation++;
    state.testVotes = { active: false, sent: 0, total: 0, remainingSeconds: 0 };
  }
  function startTestVotes() {
    const total = 50 + Math.floor(random() * 31), favorite = Math.floor(random() * state.options.length);
    const favored = Math.ceil(total * (.55 + random() * .1));
    let elapsed = 0;
    const numbers = Array.from({ length: total }, (_, i) => {
      let option = favorite;
      if (i >= favored && state.options.length > 1) {
        option = Math.floor(random() * (state.options.length - 1));
        if (option >= favorite) option++;
      }
      return option + 1;
    });
    for (let i = total - 1; i > 0; i--) { const j = Math.floor(random() * (i + 1)); [numbers[i], numbers[j]] = [numbers[j], numbers[i]]; }
    testPlan = numbers.map(number => ({ number, offset: elapsed += .3 + random() * 1.7 }));
    testPlan.forEach(item => { item.offset = item.offset / elapsed * 10000; });
    testStarted = clock(); run++;
    state.testVotes = { active: true, sent: 0, total, remainingSeconds: 10 };
  }
  function takeDueTestVotes() {
    snapshot();
    const entries = [], elapsed = clock() - testStarted;
    while (state.phase === 'voting' && state.testVotes.sent < testPlan.length && elapsed >= testPlan[state.testVotes.sent].offset) {
      const index = state.testVotes.sent++, identity = `dev-chat-${state.sessionId}-${state.round}-${run}-${index}`;
      entries.push({ platform: 'tiktok', type: 'chat', validPlatform: true, validType: true,
        user: `Viewer${index + 1}`, userDisplayName: `Viewer${index + 1}`, userId: identity,
        chatText: String(testPlan[index].number), count: 1, amount: 0, unitValue: 0, unit: '', currency: null,
        testVoteGeneration: generation, testVoteSession: state.sessionId, testVoteRound: state.round });
    }
    state.testVotes.active = state.testVotes.sent < testPlan.length;
    if (entries.length) state.revision++;
    return entries;
  }
  return { snapshot, command, vote, takeDueTestVotes, active: () => !['off', 'completed'].includes(state.phase) };
}
