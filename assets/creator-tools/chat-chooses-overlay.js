(() => {
  'use strict';
  const params = new URLSearchParams(location.search);
  const locale = params.get('lang') === 'en' ? 'en' : 'es';
  const preview = params.has('preview');
  const root = document.getElementById('event');
  const options = document.getElementById('options');
  const result = document.getElementById('result');
  const stages = ['boss', 'weapon1', 'weapon2', 'super', 'charm', 'modifier'];
  let labels;
  let cards = new Map();
  let roundKey = '';
  let resultKey = '';
  let failedAt = 0;

  function text(id, value) { document.getElementById(id).textContent = value ?? ''; }
  function voteLabel(count) { return (count === 1 ? labels.vote : labels.votes).replace('{count}', count); }
  function element(tag, className, content) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (content !== undefined) node.textContent = content;
    return node;
  }
  function picture(image) {
    const node = element('img'); node.alt = '';
    // Catalog paths are plugin-local; never load viewer-controlled URLs.
    node.src = `/assets/${image && !image.includes('..') && !image.includes(':') ? image : 'weapons/vacio.png'}`;
    return node;
  }
  function resize() {
    root.style.transform = `translate(-50%, -50%) scale(${Math.min(innerWidth / 1600, innerHeight / (root.offsetHeight + 80))})`;
  }
  function render(state) {
    if (!labels) return;
    const voting = state.phase === 'voting' || state.phase === 'reveal';
    // Keep the completed selection reviewable in the Dashboard preview only.
    // The real OBS source still hides as soon as gameplay starts.
    const final = ['result', 'countdown', 'waiting_map', 'loading'].includes(state.phase) ||
      Boolean(preview && ['active', 'completed'].includes(state.phase) && state.selected?.boss);
    root.hidden = !(voting || final) && !preview;
    root.dataset.phase = state.phase;
    options.hidden = !voting;
    result.hidden = !final;
    text('eyebrow', labels.title);
    text('title', voting ? labels.stages[state.stage] : final ? labels.phase.result : labels.phase.off);
    text('instructions', voting ? labels.voteHelp : '');
    text('status', state.phase === 'reveal' ? labels.outcome[state.outcome] : labels.phase[state.phase]);
    text('summary', voting ? voteLabel(state.totalVotes) :
      state.phase === 'countdown' ? String(state.remainingSeconds) : '');

    if (voting) {
      const key = `${state.sessionId}:${state.round}`;
      if (key !== roundKey) {
        roundKey = key; cards = new Map(); options.replaceChildren(); options.dataset.count = state.options.length;
        for (const option of state.options) {
          const card = element('article', 'choice');
          card.append(element('span', 'number', option.number), picture(option.image), element('h2', '', option.name));
          const bar = element('div', 'bar'); const fill = element('span'); bar.append(fill);
          const count = element('span', 'votes'); card.append(bar, count); options.append(card);
          cards.set(option.number, { card, fill, count });
        }
      }
      for (const option of state.options) {
        const card = cards.get(option.number); if (!card) continue;
        const percentage = state.totalVotes ? Math.round(option.votes / state.totalVotes * 100) : 0;
        card.fill.style.width = `${percentage}%`;
        card.count.textContent = `${voteLabel(option.votes)} · ${percentage}%`;
        card.card.dataset.winner = String(state.phase === 'reveal' && option.number === state.winnerNumber);
      }
    } else if (final) {
      const key = JSON.stringify(state.selected);
      if (key !== resultKey) {
        resultKey = key;
        const boss = document.getElementById('boss'); boss.replaceChildren();
        if (state.selected.boss) boss.append(picture(state.selected.boss.image), element('strong', '', state.selected.boss.name));
        const equipment = document.getElementById('equipment'); equipment.replaceChildren();
        for (const stage of stages.slice(1)) {
          if (state.plane && ['weapon1', 'weapon2', 'super'].includes(stage)) continue;
          const selected = state.selected[stage];
          const card = element('div', 'equipment');
          card.append(element('small', '', labels.stages[stage]), picture(selected?.image ?? 'creator-tools/empty.png'),
            element('strong', '', selected?.name ?? labels.withoutChallenge));
          equipment.append(card);
        }
      }
    }
    resize();
  }

  async function poll() {
    try {
      const response = await fetch('/api/config/chat-chooses', { cache: 'no-store' });
      if (!response.ok) throw new Error('unavailable');
      render(await response.json()); failedAt = 0;
    } catch {
      if (!failedAt) failedAt = Date.now();
      if (Date.now() - failedAt > 3000) { root.hidden = true; roundKey = ''; resultKey = ''; }
    }
    setTimeout(poll, 250);
  }
  window.addEventListener('resize', resize);
  fetch('/assets/creator-tools/chat-chooses-labels.json', { cache: 'no-store' })
    .then(response => response.json()).then(all => {
      labels = all[locale]; document.documentElement.lang = locale; document.title = labels.title; poll();
    });
})();
