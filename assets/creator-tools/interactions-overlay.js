(() => {
  'use strict';
  const model = window.CreatorToolsInteractionsOverlayModel;
  const artwork = window.CreatorToolsOverlayInteractions || {};
  const root = document.getElementById('interactions-overlay');
  const scene = document.getElementById('interactions-scene');
  const viewport = document.getElementById('interactions-viewport');
  const content = document.getElementById('interactions-content');
  const heading = document.getElementById('interactions-heading');
  const pagination = document.getElementById('interactions-pagination');
  const query = new URLSearchParams(location.search);
  const embedded = query.get('mode') === 'embedded' || query.get('embedded') === '1';
  const profileId = query.get('profile') === 'vertical' ? 'vertical' : 'horizontal';
  const SVG = 'http://www.w3.org/2000/svg';
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
  const brands = {
    tiktok: 'M16.6 2c.4 2.5 1.8 4 4.4 4.2v3.9a9.3 9.3 0 0 1-4.4-1.2v7.6a6.5 6.5 0 1 1-6.5-6.5h1v4a2.6 2.6 0 1 0 1.6 2.4V2h3.9Z',
    twitch: 'M4 2 1 5v16h6v3l4-3h5l7-7V2H4Zm17 11-4 4h-6l-4 4v-4H3V4h18v9ZM16 7h2v6h-2V7Zm-5 0h2v6h-2V7Z',
    youtube: 'M6 5a4 4 0 0 0-4 4v6a4 4 0 0 0 4 4h12a4 4 0 0 0 4-4V9a4 4 0 0 0-4-4H6Zm4 3v8l6-4-6-4Z',
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
  let scale = 1;
  let lastFrame = 0;
  let frame = 0;
  let pollTimer = 0;
  let disposed = false;
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
  function image(path, alt) {
    const node = element('img');
    node.src = path; node.alt = alt; node.draggable = false;
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
    node.append(element('span', 'interaction-category', labels[locale][item.category]));
    node.children[0].dataset.category = item.category;
    const art = element('div', 'interaction-art');
    const bounds = measurements.get(item.image);
    if (bounds) {
      for (const [key, value] of Object.entries(bounds)) art.style.setProperty(key === 'ratio' ? '--art-ratio' : `--image-${key}`, String(value));
    }
    const crop = element('div', 'interaction-art-crop');
    const img = image(item.image, item.name); img.className = 'interaction-art-image'; crop.append(img); art.append(crop);
    if (item.quantity > 1) art.append(element('span', 'interaction-quantity', `${item.quantity}×`));
    const name = element('h2', 'interaction-name', item.name);
    name.hidden = !view.showTitle;
    node.append(art, name);
    metrics(item.image);
    return node;
  }
  function payment(offer) {
    const node = element('div', 'interaction-payment');
    node.dataset.platform = offer.platform;
    const denomination = offer.giftName || labels[locale][offer.eventType];
    node.setAttribute('aria-label', `${platformNames[offer.platform]}: ${offer.every} ${denomination}${offer.rewardName ? ` · ${offer.rewardName}` : ''}`);
    node.append(element('span', 'interaction-brand', platformNames[offer.platform]));
    const icon = element('span', 'interaction-currency');
    if (offer.image) icon.append(image(offer.image, offer.giftName));
    else if (offer.eventType === 'jewels') icon.append(image('/assets/creator-tools/youtube/jewels.png', labels[locale].jewels));
    else icon.append(svgIcon(paths[offer.eventType] || paths.gift));
    const mark = element('span', 'interaction-platform-mark'); mark.append(svgIcon([brands[offer.platform]], true)); icon.append(mark);
    node.append(icon, element('span', 'interaction-cost', String(offer.every)));
    if (offer.rewardName) node.append(element('span', 'interaction-reward', offer.rewardName));
    return node;
  }
  function card(item) {
    const node = element('article', 'interaction-item');
    node.setAttribute('aria-label', item.name);
    node.dataset.interaction = item.interaction;
    if (view.interactionsTemplate === 'cards') {
      node.style.width = `${Math.max(204, (measurements.get(item.image)?.ratio || 1) * 70 + 22)}px`;
    }
    const payments = element('div', 'interaction-payments');
    const size = model.paymentSize(item.offers.length, view.interactionsAdaptive, view.interactionsTemplate);
    node.style.setProperty('--currency', `${size}px`);
    node.style.setProperty('--value-size', `${Math.round(size * .58)}px`);
    for (const offer of item.offers) payments.append(payment(offer));
    node.append(effect(item), payments);
    return node;
  }
  function cycle() {
    const node = element('div', 'interactions-cycle');
    for (const item of allItems) node.append(card(item));
    return node;
  }
  function drawPagination() {
    pagination.replaceChildren();
    if (allPages.length <= 1) return;
    for (let index = 0; index < allPages.length; index++) {
      const dot = element('span', 'interaction-dot'); dot.dataset.current = String(index === page); pagination.append(dot);
    }
  }
  function drawContent() {
    if (disposed || root.hidden) return;
    content.style.transform = '';
    if (view.interactionsLayout === 'pages') {
      content.replaceChildren(...(allPages[page] || []).map(item => {
        const cell = element('div', 'interaction-cell'); cell.append(card(item)); return cell;
      }));
      root.dataset.page = String(page);
      drawPagination();
    } else {
      content.replaceChildren(cycle());
      pagination.replaceChildren();
    }
    fit();
  }
  function fit() {
    if (disposed || root.hidden) return;
    const width = root.clientWidth || window.innerWidth || 1;
    const height = root.clientHeight || window.innerHeight || 1;
    scene.style.width = ''; scene.style.transform = ''; viewport.style.width = '';
    if (view.interactionsLayout === 'pages') {
      const inset = view.interactionsTemplate === 'cards' ? 26 : 0;
      const naturalWidth = Math.max(content.getBoundingClientRect?.()?.width || 0,
        content.offsetWidth || 0, content.scrollWidth || 1) + inset;
      scene.style.width = `${naturalWidth}px`;
      viewport.style.width = `${Math.max(1, naturalWidth - inset)}px`;
      const naturalHeight = Math.max(scene.getBoundingClientRect?.()?.height || 0,
        scene.offsetHeight || 0, scene.scrollHeight || content.scrollHeight || 1);
      scale = Math.min(width / naturalWidth, height / naturalHeight, 4);
      scene.style.left = `${Math.max(0, (width - naturalWidth * scale) / 2)}px`;
      scene.style.top = `${Math.max(0, (height - naturalHeight * scale) / 2)}px`;
    } else {
      const first = content.children[0];
      if (!first) return;
      const naturalHeight = Math.max(scene.getBoundingClientRect?.()?.height || 0,
        scene.offsetHeight || 0, scene.scrollHeight || 1);
      scale = Math.min(height / naturalHeight, 4);
      const inset = view.interactionsTemplate === 'cards' ? 26 : 0;
      const viewportWidth = Math.max(1, width / scale - inset);
      viewport.style.width = `${viewportWidth}px`;
      scene.style.width = `${width / scale}px`;
      scene.style.left = '0px'; scene.style.top = `${Math.max(0, (height - naturalHeight * scale) / 2)}px`;
      cycleWidth = first.offsetWidth || first.scrollWidth || 1;
      const required = model.repetitions(cycleWidth, viewportWidth);
      while (content.children.length < required) {
        const copy = cycle(); copy.setAttribute('aria-hidden', 'true'); content.append(copy);
      }
      while (content.children.length > required) content.lastElementChild.remove();
      stripOffset %= cycleWidth;
      content.style.transform = `translateX(${-stripOffset}px)`;
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
    root.dataset.showTitle = String(view.showTitle);
    root.style.setProperty('--ink', view.textColor);
    window.CreatorToolsOverlayText?.apply(root, view);
    allPages = model.pages(allItems);
    const retainedPage = previousKey ? allPages.findIndex(entries => entries.some(entry => entry.key === previousKey)) : -1;
    page = retainedPage >= 0 ? retainedPage : Math.min(page, Math.max(0, allPages.length - 1));
    pageElapsed = 0;
    heading.replaceChildren(element('span', '', labels[locale].title), element('span', 'interaction-network-list',
      model.selectedNetworks(view).map(network => platformNames[network]).join(' · ')));
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
          page = model.nextPage(page, allPages.length); drawContent();
        }
      } else if (view.motion && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
        stripOffset = model.offset(stripOffset, view.interactionsSpeed, elapsed, cycleWidth);
        content.style.transform = `translateX(${-stripOffset}px)`;
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
