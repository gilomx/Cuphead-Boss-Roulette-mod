(() => {
  'use strict';
  const model = window.CreatorToolsInteractionsOverlayModel;
  const artwork = window.CreatorToolsOverlayInteractions || {};
  const root = document.getElementById('interactions-overlay');
  const scene = document.getElementById('interactions-scene');
  const viewport = document.getElementById('interactions-viewport');
  const content = document.getElementById('interactions-content');
  const query = new URLSearchParams(location.search);
  const embedded = query.get('mode') === 'embedded' || query.get('embedded') === '1';
  const profileId = query.get('profile') === 'vertical' ? 'vertical' : 'horizontal';
  const SVG = 'http://www.w3.org/2000/svg';
  const stripGap = 20;
  const stripInset = 12;
  const stripCapacity = 4;
  const platformNames = { tiktok: 'TikTok', twitch: 'Twitch', youtube: 'YouTube' };
  const labels = {
    es: { title: 'Interacciones', attack: 'Ataque', challenge: 'Reto', help: 'Ayuda',
      gift: 'regalos', like: 'me gusta', follow: 'seguimientos', currency: 'Bits', jewels: 'gemas',
      subscription: 'suscripciones', subscription_gift: 'suscripciones regaladas', resubscription: 'resuscripciones', redemption: 'canjes' },
    en: { title: 'Interactions', attack: 'Attack', challenge: 'Challenge', help: 'Help',
      gift: 'gifts', like: 'likes', follow: 'follows', currency: 'Bits', jewels: 'jewels',
      subscription: 'subscriptions', subscription_gift: 'gift subscriptions', resubscription: 'resubscriptions', redemption: 'redemptions' },
  };
  const paths = {
    gift: ['M3 8h18v4H3z', 'M5 12v9h14v-9M12 8v13', 'M12 8C4 8 5 1 8 3c2 1 4 5 4 5Zm0 0c8 0 7-7 4-5-2 1-4 5-4 5Z'],
    like: ['M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8Z'],
    follow: ['M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2', 'M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z', 'M20 8v6M17 11h6'],
    currency: ['M12 2 3 10l9 12 9-12-9-8Z', 'M3 10h18M12 2 8 10l4 12 4-12-4-8Z'],
    subscription: ['m12 2 3 6 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1 3-6Z'],
    subscription_gift: ['M3 8h18v4H3zM5 12v9h14v-9M12 8v13', 'm12 1 2 3 4 .5-3 2 .5 2-3.5-1-3.5 1 .5-2-3-2L10 4l2-3Z'],
    resubscription: ['M20 7v5h-5M4 17v-5h5', 'M6 7a7 7 0 0 1 12-2l2 2M4 17l2 2a7 7 0 0 0 12-2'],
    redemption: ['M3 5h18v4a3 3 0 0 0 0 6v4H3v-4a3 3 0 0 0 0-6V5ZM13 5v2m0 3v2m0 3v2'],
  };
  let catalog = {};
  let latestState = null;
  let view = model.presentation();
  let locale = 'es';
  let allItems = [];
  let allPages = [];
  let page = 0;
  let pageElapsed = 0;
  let lastSignature = '';
  let stripOffset = 0;
  let cycleWidth = 0;
  let pageWidth = 0;
  let scale = 1;
  let lastFrame = 0;
  let frame = 0;
  let pollTimer = 0;
  let disposed = false;
  let measuredCards = [];
  const measurements = new Map();
  const measuring = new Map();
  let metricRenderQueued = false;
  document.documentElement.dataset.overlayMode = embedded ? 'embedded' : 'standalone';

  function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  }
  function image(path) {
    const node = element('img');
    // The enclosing card/payment supplies its accessible label. Even a failed
    // image must not turn an item or reward name into visible fallback text.
    node.src = path; node.alt = ''; node.draggable = false;
    return node;
  }
  function svgIcon(pathList, filled = false) {
    const svg = document.createElementNS(SVG, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');
    if (filled) svg.setAttribute('fill-rule', 'evenodd');
    for (const path of pathList) {
      const node = document.createElementNS(SVG, 'path'); node.setAttribute('d', path); svg.append(node);
    }
    return svg;
  }
  function metrics(path) {
    if (measurements.has(path) || measuring.has(path)) return;
    const probe = new Image();
    measuring.set(path, probe);
    probe.onload = () => {
      try {
        const canvas = document.createElement('canvas');
        canvas.width = probe.naturalWidth; canvas.height = probe.naturalHeight;
        const context = canvas.getContext('2d', { willReadFrequently: true });
        if (!context || !canvas.width || !canvas.height) return;
        context.drawImage(probe, 0, 0);
        measurements.set(path, model.opaqueBounds(canvas.width, canvas.height,
          context.getImageData(0, 0, canvas.width, canvas.height).data));
      } catch { /* Local image decoding failures fall back to its natural aspect ratio. */ }
      finally {
        if (!measurements.has(path)) measurements.set(path, {
          ratio: probe.naturalWidth / (probe.naturalHeight || probe.naturalWidth || 1),
          width: probe.naturalWidth / (probe.naturalHeight || probe.naturalWidth || 1), height: 1, left: 0, top: 0,
        });
        measuring.delete(path);
        if (!metricRenderQueued && !disposed) {
          metricRenderQueued = true;
          requestAnimationFrame(() => {
            metricRenderQueued = false;
            if (!disposed && !root.hidden) drawContent();
          });
        }
      }
    };
    probe.onerror = () => { measuring.delete(path); measurements.set(path, { ratio: 1, width: 1, height: 1, left: 0, top: 0 }); };
    probe.src = path;
  }
  function effect(item) {
    const node = element('div', 'interaction-effect');
    const art = element('div', 'interaction-art');
    const bounds = measurements.get(item.image);
    if (bounds) {
      for (const [key, value] of Object.entries(model.containArtwork(bounds))) {
        const property = key === 'cropWidth' ? '--crop-width' : key === 'cropHeight' ? '--crop-height' : `--image-${key}`;
        art.style.setProperty(property, String(value));
      }
    }
    const crop = element('div', 'interaction-art-crop');
    const img = image(item.image); img.className = 'interaction-art-image'; crop.append(img); art.append(crop);
    if (item.quantity > 1) art.append(element('span', 'interaction-quantity', `${item.quantity}×`));
    node.append(art);
    metrics(item.image);
    return node;
  }
  function payment(offer) {
    const node = element('div', 'interaction-payment');
    node.dataset.platform = offer.platform;
    const denomination = offer.giftName || labels[locale][offer.eventType];
    node.setAttribute('aria-label', `${platformNames[offer.platform]}: ${offer.every} ${denomination}${offer.rewardName ? ` · ${offer.rewardName}` : ''}`);
    const icon = element('span', 'interaction-currency');
    if (offer.image) icon.append(image(offer.image));
    else if (offer.platform === 'twitch' && offer.eventType === 'currency') icon.append(image('/assets/creator-tools/twitch/bits.png'));
    else if (offer.eventType === 'jewels') icon.append(image('/assets/creator-tools/youtube/jewels.png'));
    else icon.append(svgIcon(paths[offer.eventType] || paths.gift));
    node.append(icon, element('span', 'interaction-cost', String(offer.every)));
    return node;
  }
  function card(item, measure = true) {
    const node = element('article', 'interaction-item');
    node.setAttribute('aria-label', item.name);
    node.dataset.interaction = item.interaction;
    // Keep a stable, different part of the paper grain on each item and its loop copies.
    const grainSeed = Array.from(item.key).reduce((hash, character) => (hash * 31 + character.charCodeAt(0)) >>> 0, 17);
    node.style.setProperty('--card-grain-position', `${grainSeed % 397}px ${Math.floor(grainSeed / 397) % 313}px`);
    const payments = element('div', 'interaction-payments');
    const size = model.paymentSize(item.offers.length, view.interactionsAdaptive, view.interactionsTemplate);
    node.style.setProperty('--currency', `${size}px`);
    node.style.setProperty('--value-size', `${Math.round(size * .58)}px`);
    for (const offer of item.offers) payments.append(payment(offer));
    node.append(effect(item), payments);
    if (measure) measuredCards.push(node);
    return node;
  }
  function cycle(duplicate = false) {
    const node = element('div', 'interactions-cycle');
    for (const item of allItems) node.append(card(item, !duplicate));
    return node;
  }
  function movePage(animate) {
    const motion = animate && view.motion && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    content.style.transition = motion ? 'transform 380ms cubic-bezier(.22,.68,0,1)' : 'none';
    content.style.transform = `translateX(${-page * pageWidth}px)`;
    root.dataset.page = String(page);
  }
  function drawContent() {
    if (disposed || root.hidden) return;
    content.style.transform = '';
    content.style.transition = 'none';
    measuredCards = [];
    if (view.interactionsLayout === 'pages') {
      content.replaceChildren(...allPages.map(items => {
        const sheet = element('div', 'interactions-page');
        for (const item of items) {
          const cell = element('div', 'interaction-cell'); cell.append(card(item)); sheet.append(cell);
        }
        return sheet;
      }));
    } else {
      content.replaceChildren(cycle());
    }
    fit();
  }
  function sizeCards() {
    root.style.setProperty('--item-width', 'max-content');
    root.style.setProperty('--item-height', 'auto');
    // Normalize across the entire catalog, including pages not currently visible.
    const width = Math.ceil(Math.max(1, ...measuredCards.map(card => card.offsetWidth || card.scrollWidth || 1)));
    const height = Math.ceil(Math.max(1, ...measuredCards.map(card => card.offsetHeight || card.scrollHeight || 1)));
    root.style.setProperty('--item-width', `${width}px`);
    root.style.setProperty('--item-height', `${height}px`);
    return { width, height };
  }
  function fitPages(width, height) {
    const sheets = Array.from(content.children);
    for (const sheet of sheets) { sheet.style.width = ''; sheet.style.height = ''; }
    pageWidth = Math.max(1, ...sheets.map(sheet => Math.max(sheet.offsetWidth || 0, sheet.scrollWidth || 0)));
    const naturalHeight = Math.max(1, ...sheets.map(sheet => Math.max(sheet.offsetHeight || 0, sheet.scrollHeight || 0)));
    for (const sheet of sheets) { sheet.style.width = `${pageWidth}px`; sheet.style.height = `${naturalHeight}px`; }
    scene.style.width = `${pageWidth}px`;
    viewport.style.width = `${pageWidth}px`;
    viewport.style.height = `${naturalHeight}px`;
    scale = view.interactionsSize > 0
      ? Math.min(width / pageWidth, height * view.interactionsSize / 100 / naturalHeight)
      : Math.min(width / pageWidth, height / naturalHeight, 4);
    scene.style.left = `${Math.max(0, (width - pageWidth * scale) / 2)}px`;
    scene.style.top = `${Math.max(0, (height - naturalHeight * scale) / 2)}px`;
    movePage(false);
  }
  function fitStrip(width, height, size) {
    if (!content.children[0]) return;
    const visibleCount = Math.min(stripCapacity, allItems.length);
    let naturalWidth = visibleCount * size.width + (visibleCount - 1) * stripGap + stripInset * 2;
    const naturalHeight = size.height + stripInset * 2;
    const moving = allItems.length > stripCapacity;
    if (view.interactionsSize > 0) {
      // Small groups must fit completely. Moving strips can show fewer/larger
      // items, but even at maximum height one full item must fit horizontally.
      scale = Math.min(height * view.interactionsSize / 100 / naturalHeight,
        width / (moving ? size.width + stripInset * 2 : naturalWidth));
      if (moving) naturalWidth = width / scale;
    } else scale = Math.min(width / naturalWidth, height / naturalHeight, 4);
    viewport.style.width = `${naturalWidth}px`;
    viewport.style.height = `${naturalHeight}px`;
    scene.style.width = `${naturalWidth}px`;
    scene.style.left = `${Math.max(0, (width - naturalWidth * scale) / 2)}px`;
    scene.style.top = `${Math.max(0, (height - naturalHeight * scale) / 2)}px`;
    // A whole cycle includes its trailing gap, matching the gap between copies.
    cycleWidth = allItems.length * (size.width + stripGap);
    root.dataset.scrolling = String(moving);
    const required = moving ? model.repetitions(cycleWidth, naturalWidth) : 1;
    while (content.children.length < required) {
      const copy = cycle(true); copy.setAttribute('aria-hidden', 'true'); content.append(copy);
    }
    while (content.children.length > required) content.lastElementChild.remove();
    stripOffset = moving ? stripOffset % cycleWidth : 0;
    content.style.transform = `translateX(${stripInset - stripOffset}px)`;
  }
  function fit() {
    if (disposed || root.hidden) return;
    const width = root.clientWidth || window.innerWidth || 1;
    const height = root.clientHeight || window.innerHeight || 1;
    scene.style.width = ''; scene.style.transform = ''; viewport.style.width = ''; viewport.style.height = '';
    const size = sizeCards();
    if (view.interactionsLayout === 'pages') {
      fitPages(width, height);
    } else {
      fitStrip(width, height, size);
    }
    scene.style.transform = `scale(${scale})`;
  }
  function render(state) {
    latestState = state;
    locale = String(state.locale || query.get('locale') || 'es').startsWith('en') ? 'en' : 'es';
    document.documentElement.lang = locale;
    document.title = `${labels[locale].title} — La Pichi Ruleta`;
    const normalized = model.presentation(state.presentation);
    const nextItems = model.items(state, normalized, catalog, artwork, locale);
    const visible = (state.preview === true || (state.visible !== false && state.enabled !== false && state.active !== false)) && nextItems.length > 0;
    const signature = JSON.stringify([visible, normalized, nextItems, locale]);
    if (signature === lastSignature) return;
    const previousKey = allPages[page]?.[0]?.key;
    lastSignature = signature; view = normalized; allItems = nextItems;
    root.hidden = !visible;
    root.dataset.template = view.interactionsTemplate;
    root.dataset.layout = view.interactionsLayout;
    root.dataset.count = String(allItems.length);
    root.style.setProperty('--ink', view.textColor);
    root.style.setProperty('--paper', view.interactionsCardColor);
    root.style.setProperty('--card-noise', String(view.interactionsCardNoise / 100));
    window.CreatorToolsOverlayText?.apply(root, view);
    allPages = model.pages(allItems);
    const retainedPage = previousKey ? allPages.findIndex(entries => entries.some(entry => entry.key === previousKey)) : -1;
    page = retainedPage >= 0 ? retainedPage : Math.min(page, Math.max(0, allPages.length - 1));
    pageElapsed = 0;
    if (!visible) { content.replaceChildren(); return; }
    drawContent();
  }
  function tick(time) {
    if (disposed) return;
    const elapsed = lastFrame ? Math.min(1000, Math.max(0, time - lastFrame)) : 0;
    lastFrame = time;
    if (!root.hidden) {
      if (view.interactionsLayout === 'pages') {
        pageElapsed += elapsed;
        if (allPages.length > 1 && pageElapsed >= view.interactionsPageSeconds * 1000) {
          pageElapsed %= view.interactionsPageSeconds * 1000;
          page = model.nextPage(page, allPages.length); movePage(true);
        }
      } else if (allItems.length > stripCapacity && view.motion && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
        stripOffset = model.offset(stripOffset, view.interactionsSpeed, elapsed, cycleWidth);
        content.style.transform = `translateX(${stripInset - stripOffset}px)`;
      }
    }
    frame = requestAnimationFrame(tick);
  }
  async function fetchJson(url) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 3500);
    try {
      const response = await fetch(url, { cache: 'no-store', signal: controller.signal });
      if (!response.ok) throw new Error('unavailable');
      return await response.json();
    } finally { clearTimeout(timeout); }
  }
  async function poll() {
    if (disposed) return;
    try {
      const [state, config] = await Promise.all([
        fetchJson('/api/config/interactions/rules'), fetchJson('/api/overlay-composer/config'),
      ]);
      if (disposed) return;
      const profile = config.profiles?.find(entry => entry.id === profileId);
      const component = profile?.components?.find(entry => entry.id === 'interactions');
      render({ ...state, visible: component?.enabled !== false, presentation: component || {}, locale: query.get('locale') || 'es' });
    } catch {
      // A disconnected overlay must not keep advertising stale prices indefinitely.
      if (!disposed) { root.hidden = true; lastSignature = ''; }
    }
    if (!disposed) pollTimer = setTimeout(poll, 1000);
  }
  function message(event) {
    const message = event.data;
    if (event.source !== window.parent || event.origin !== location.origin ||
        message?.type !== 'creator-tools-overlay-preview' || message.version !== 1 ||
        message.overlay !== 'interactions' || !message.state || typeof message.state !== 'object') return;
    render(message.state);
  }
  function dispose() {
    disposed = true; clearTimeout(pollTimer); cancelAnimationFrame(frame);
    window.removeEventListener('message', message); window.removeEventListener('resize', fit);
    for (const probe of measuring.values()) { probe.onload = null; probe.onerror = null; }
    measuring.clear();
  }
  window.addEventListener('resize', fit);
  window.addEventListener('pagehide', dispose, { once: true });
  if (embedded) window.addEventListener('message', message);
  frame = requestAnimationFrame(tick);
  fetchJson('/assets/creator-tools/gifts/catalog.json').then(value => { catalog = value; })
    .catch(() => { catalog = {}; })
    .finally(() => {
      if (disposed) return;
      if (latestState) { lastSignature = ''; render(latestState); }
      if (embedded) window.parent.postMessage({ type: 'creator-tools-overlay-preview-ready', version: 1,
        overlay: 'interactions', mode: 'embedded' }, location.origin);
      else poll();
    });
})();
