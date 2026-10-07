(() => {
  'use strict';
  const params = new URLSearchParams(location.search);
  let locale = (params.get('locale') || params.get('lang')) === 'en' ? 'en' : 'es';
  const preview = params.has('preview');
  const embedded = params.get('embedded') === '1' && !preview;
  const root = document.getElementById('event');
  const options = document.getElementById('options');
  const result = document.getElementById('result');
  const heading = document.getElementById('heading');
  const stages = ['boss', 'weapon1', 'weapon2', 'super', 'charm', 'modifier'];
  const challengeArtwork = {
    blacknwhite: 'blanco-y-negro', upside_down: 'pantalla-invertida', rgb: 'pantalla-rgb',
    nopeashooter: 'sin-peashooter', nomini: 'sin-miniavion', noex: 'sin-ex', nodash: 'sin-dash',
    nobombs: 'sin-bombas', mini: 'solo-miniavion', locked: 'modo-tieso', inkrain: 'lluvia-de-tinta',
    hp1: 'una-vida', halfdamage: 'mitad-de-dano',
  };
  let labels;
  let allLabels;
  let latestState;
  let cards = new Map();
  let sceneKey = '';
  let leaving = false;
  let transitionEpoch = 0;
  let transitionTimer = 0;
  let sceneAnimations = [];
  let failedAt = 0;

  function text(id, value) { document.getElementById(id).textContent = value ?? ''; }
  function voteLabel(count) { return (count === 1 ? labels.vote : labels.votes).replace('{count}', count); }
  function voteCount(count) { return count < 1000 ? String(count) :
    new Intl.NumberFormat(locale, { notation: 'compact', maximumFractionDigits: 1 }).format(count); }
  function element(tag, className, content) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (content !== undefined) node.textContent = content;
    return node;
  }
  function picture(image) {
    // Catalog paths are plugin-local; never load viewer-controlled URLs.
    let path = typeof image === 'string' && !image.includes('..') && !image.includes(':') &&
      !image.includes('\\') && !image.startsWith('/') ? image.toLowerCase() : 'weapons/vacio.png';
    // Keep raw equipment portraits; this overlay has its own edited challenge artwork.
    if (path === 'weapons/vacio.png') path = 'creator-tools/empty.png';
    else if (/^creator-tools\/(weapons|supers|charms|modifiers)\//.test(path)) path = path.slice('creator-tools/'.length);
    const challenge = /^modifiers\/([a-z0-9_]+)_01\.png$/.exec(path);
    if (challenge && Object.hasOwn(challengeArtwork, challenge[1]))
      path = `creator-tools/chat-chooses-modifiers/${challengeArtwork[challenge[1]]}.png`;
    const artwork = element('div', 'artwork');
    const node = element('img'); node.alt = ''; node.src = `/assets/${path}`;
    artwork.append(node);
    return artwork;
  }
  function resize() {
    if (root.hidden) return;
    root.style.transform = `translate(-50%, -50%) scale(${Math.min(innerWidth / root.offsetWidth, innerHeight / root.offsetHeight)})`;
  }
  function motion(state) {
    return state.presentation?.motion !== false && !window.matchMedia('(prefers-reduced-motion: reduce)').matches &&
      typeof root.animate === 'function';
  }
  function stopAnimations(animations) {
    for (const animation of animations) animation.cancel();
  }
  function cancelTransition() {
    transitionEpoch++;
    clearTimeout(transitionTimer); transitionTimer = 0;
    stopAnimations(sceneAnimations); sceneAnimations = []; leaving = false;
    for (const entry of cards.values()) {
      entry.countEpoch++; stopAnimations(entry.countAnimations); entry.countAnimations = [];
      if (entry.label !== undefined) entry.count.replaceChildren(element('span', 'vote-digit', entry.label));
    }
  }
  function describe(state) {
    const voting = state.phase === 'voting' || state.phase === 'reveal';
    const final = ['result', 'loading'].includes(state.phase) ||
      Boolean(preview && ['active', 'completed'].includes(state.phase) && state.selected?.boss);
    const key = voting ? `round:${state.sessionId}:${state.round}:${state.stage}:${JSON.stringify(state.options.map(option => [option.number, option.id, option.image]))}` :
      final ? `result:${state.sessionId}:${state.plane}:${JSON.stringify(state.selected)}` : '';
    return { key, voting, final };
  }
  function animateCount(entry, votes, state) {
    const label = voteCount(votes);
    entry.count.title = voteLabel(votes);
    entry.count.setAttribute('aria-label', voteLabel(votes));
    entry.count.dataset.wide = String(label.length > 2);
    if (entry.votes === votes) return;
    const oldLabel = entry.label;
    const epoch = ++entry.countEpoch;
    stopAnimations(entry.countAnimations); entry.countAnimations = [];
    const next = element('span', 'vote-digit', label);
    entry.votes = votes; entry.label = label;
    if (oldLabel === undefined || !motion(state)) { entry.count.replaceChildren(next); return; }
    const old = element('span', 'vote-digit', oldLabel);
    entry.count.replaceChildren(old, next);
    const outgoing = old.animate([{ opacity: 1, transform: 'translateY(0)' }, { opacity: 0, transform: 'translateY(-45%)' }],
      { duration: 220, easing: 'cubic-bezier(.22, 1, .36, 1)', fill: 'forwards' });
    const incoming = next.animate([{ opacity: 0, transform: 'translateY(45%)' }, { opacity: 1, transform: 'translateY(0)' }],
      { duration: 260, easing: 'cubic-bezier(.22, 1, .36, 1)', fill: 'both' });
    entry.countAnimations = [outgoing, incoming];
    outgoing.finished.catch(() => {});
    incoming.finished.then(() => {
      if (entry.countEpoch !== epoch) return;
      entry.count.replaceChildren(next);
      stopAnimations(entry.countAnimations); entry.countAnimations = [];
    }, () => {});
  }
  function sceneNodes() {
    const content = options.hidden ? [document.getElementById('boss'), ...document.getElementById('equipment').children] :
      [...cards.values()].map(entry => entry.card);
    return [...(heading.hidden ? [] : [heading]), ...content];
  }
  function enter(state) {
    if (!motion(state)) return;
    sceneAnimations = sceneNodes().map((node, index) => {
      const animation = node.animate([{ opacity: 0, transform: 'translateY(12px) scale(.86)' }, { opacity: getComputedStyle(node).opacity, transform: 'none' }],
        { duration: 420, delay: index * 80, easing: 'cubic-bezier(.22, 1, .36, 1)', fill: 'backwards' });
      animation.finished.catch(() => {});
      return animation;
    });
  }
  function presentation(state) {
    const view = state.presentation || {};
    window.CreatorToolsOverlayText?.apply(root, view);
    const color = (value, fallback) => /^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(String(value)) ? value : fallback;
    root.style.setProperty('--choice-color', color(view.liquidColor, '#ff4f92'));
    root.style.setProperty('--winner-color', color(view.collectingColor, '#f4c95d'));
    root.style.setProperty('--text-color', color(view.textColor, '#ffffff'));
    root.style.setProperty('--outline-color', color(view.outlineColor, '#d3af93'));
    root.style.setProperty('--vote-outline-color', color(view.voteOutlineColor, '#ffffff'));
    root.dataset.showTitle = String(view.showTitle !== false);
    root.dataset.showDetails = String(view.showDetails !== false);
    root.dataset.motion = String(view.motion !== false);
  }
  function render(state) {
    latestState = state;
    if (!labels) return;
    const nextLocale = state.locale === 'en' ? 'en' : state.locale === 'es' ? 'es' : locale;
    if (locale !== nextLocale) { locale = nextLocale; labels = allLabels[locale]; }
    document.documentElement.lang = locale;
    presentation(state);
    const scene = describe(state);
    // Voting cancellation still wins immediately. The final selection keeps
    // its portraits mounted until their staggered exit has finished.
    if (!scene.key && !sceneKey.startsWith('result:')) {
      hide(); return;
    }
    if (!motion(state)) {
      cancelTransition(); commit(state, scene); return;
    }
    if (leaving) return; // latestState keeps the newest round and its current votes.
    if (!sceneKey || sceneKey === scene.key) { commit(state, scene); return; }
    leaving = true;
    const epoch = ++transitionEpoch;
    const nodes = sceneNodes();
    const starts = nodes.map(node => ({ opacity: getComputedStyle(node).opacity, transform: getComputedStyle(node).transform }));
    stopAnimations(sceneAnimations);
    sceneAnimations = nodes.map((node, index) => node.animate([
      starts[index], { opacity: 0, transform: 'translateY(-10px) scale(.86)' },
    ], { duration: 240, delay: index * 60, easing: 'cubic-bezier(.4, 0, 1, 1)', fill: 'forwards' }));
    const finish = () => {
      if (epoch !== transitionEpoch || !leaving) return;
      cancelTransition(); sceneKey = '';
      commit(latestState, describe(latestState));
    };
    // OBS can throttle animation completion while hidden; never leave a round stuck.
    transitionTimer = setTimeout(finish, 240 + Math.max(0, nodes.length - 1) * 60 + 80);
    Promise.allSettled(sceneAnimations.map(animation => animation.finished)).then(finish);
  }
  function hide() {
    cancelTransition(); sceneKey = ''; root.hidden = true; cards = new Map();
    options.replaceChildren(); result.hidden = true;
  }
  function commit(state, scene) {
    if (!scene.key) { hide(); return; }
    const { voting, final } = scene;
    const changed = sceneKey !== scene.key;
    root.hidden = false;
    root.dataset.phase = state.phase;
    options.hidden = !voting;
    result.hidden = !final;
    heading.hidden = !voting || state.presentation?.showTitle === false;
    text('voting-title', voting ? labels.votingTitle : '');
    text('title', voting ? labels.stages[state.stage] : '');

    if (voting) {
      if (changed) {
        for (const entry of cards.values()) { entry.countEpoch++; stopAnimations(entry.countAnimations); }
        cards = new Map(); options.replaceChildren(); options.dataset.count = state.options.length;
        options.style.setProperty('--option-count', String(state.options.length));
        for (const option of state.options) {
          const card = element('article', 'choice');
          card.setAttribute('aria-label', option.name);
          const portrait = element('div', 'portrait');
          const count = element('span', 'votes');
          portrait.append(picture(option.image), element('span', 'number', option.number), count);
          card.append(portrait); options.append(card);
          cards.set(option.number, { card, count, countEpoch: 0, countAnimations: [] });
        }
      }
      const highestVotes = Math.max(0, ...state.options.map(option => option.votes || 0));
      for (const option of state.options) {
        const card = cards.get(option.number); if (!card) continue;
        card.card.setAttribute('aria-label', option.name);
        const votes = option.votes || 0;
        animateCount(card, votes, state);
        card.card.dataset.leader = String(state.phase === 'voting' && highestVotes > 0 && votes === highestVotes);
        card.card.dataset.winner = String(state.phase === 'reveal' && option.number === state.winnerNumber);
      }
    } else if (final) {
      if (changed) {
        const boss = document.getElementById('boss'); boss.replaceChildren();
        if (state.selected.boss) boss.append(picture(state.selected.boss.image));
        const equipment = document.getElementById('equipment'); equipment.replaceChildren();
        for (const stage of stages.slice(1)) {
          if (state.plane && ['weapon1', 'weapon2', 'super'].includes(stage)) continue;
          const selected = state.selected[stage];
          const card = element('div', 'equipment');
          card.setAttribute('aria-label', labels.stages[stage]);
          card.append(picture(selected?.image ?? 'creator-tools/empty.png'));
          equipment.append(card);
        }
      }
    }
    sceneKey = scene.key;
    resize();
    if (changed) enter(state);
  }

  async function poll() {
    try {
      const response = await fetch('/api/config/chat-chooses', { cache: 'no-store' });
      if (!response.ok) throw new Error('unavailable');
      render(await response.json()); failedAt = 0;
    } catch {
      if (!failedAt) failedAt = Date.now();
      if (Date.now() - failedAt > 3000) { cancelTransition(); root.hidden = true; sceneKey = ''; }
    }
    setTimeout(poll, 250);
  }
  window.addEventListener('resize', resize);
  if (embedded) {
    window.addEventListener('message', event => {
      const message = event.data;
      if (event.source !== window.parent || event.origin !== location.origin ||
          message?.type !== 'creator-tools-overlay-preview' || message.version !== 1 ||
          message.overlay !== 'chat-chooses' || !message.state || typeof message.state !== 'object') return;
      render(message.state);
    });
  }
  fetch('/assets/creator-tools/chat-chooses-labels.json', { cache: 'no-store' })
    .then(response => response.json()).then(all => {
      allLabels = all; labels = all[locale]; document.documentElement.lang = locale; document.title = labels.title;
      if (embedded) {
        if (latestState) render(latestState);
        window.parent.postMessage({ type: 'creator-tools-overlay-preview-ready', version: 1, overlay: 'chat-chooses', mode: 'embedded' }, location.origin);
      } else poll();
    });
})();
