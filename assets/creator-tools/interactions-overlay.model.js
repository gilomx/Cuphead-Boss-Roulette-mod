/* Shared, side-effect-free projection of saved stream rules into overlay items. */
(function (host) {
  'use strict';
  const networks = Object.freeze(['tiktok', 'twitch', 'youtube']);
  const triggers = Object.freeze({
    tiktok: ['gift', 'like', 'follow'],
    twitch: ['currency', 'follow', 'subscription', 'subscription_gift', 'resubscription', 'redemption'],
    youtube: ['jewels'],
  });
  const number = (value, fallback, min, max) => {
    const parsed = Number(value);
    return Math.min(max, Math.max(min, Number.isFinite(parsed) ? parsed : fallback));
  };
  function presentation(source = {}) {
    return {
      interactionsTemplate: source.interactionsTemplate === 'cards' ? 'cards' : 'floating',
      interactionsLayout: source.interactionsLayout === 'strip' ? 'strip' : 'pages',
      interactionsTikTok: source.interactionsTikTok !== false,
      interactionsTwitch: source.interactionsTwitch !== false,
      interactionsYouTube: source.interactionsYouTube !== false,
      interactionsPageSeconds: Math.round(number(source.interactionsPageSeconds, 8, 3, 60)),
      interactionsSpeed: number(source.interactionsSpeed, 40, 40, 140),
      interactionsCardColor: /^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(source.interactionsCardColor || '')
        ? source.interactionsCardColor : '#23262bee',
      interactionsCardNoise: Math.round(number(source.interactionsCardNoise ?? 18, 18, 0, 100)),
      interactionsSize: Number(source.interactionsSize) > 0 ? Math.round(number(source.interactionsSize, 75, 25, 100)) : 0,
      interactionsAdaptive: source.interactionsAdaptive !== false,
      // Older scenes may still request labels. This overlay is always image + cost.
      showTitle: false,
      motion: source.motion !== false,
      textColor: /^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(source.textColor || '') ? source.textColor : '#fff4dc',
      textFont: ['clean', 'system', 'rounded', 'serif'].includes(source.textFont) ? source.textFont : 'clean',
      textWeight: [400, 700, 900].includes(source.textWeight) ? source.textWeight : 700,
      textShadowColor: /^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(source.textShadowColor || '') ? source.textShadowColor : '#00000000',
      textShadowX: Math.round(number(source.textShadowX, 2, -20, 20)),
      textShadowY: Math.round(number(source.textShadowY, 3, -20, 20)),
      textShadowBlur: Math.round(number(source.textShadowBlur, 2, 0, 20)),
    };
  }
  function floatingLayout(profile) {
    // Confirmed in the live designer; the temporary controls are retired.
    return profile === 'vertical' ? { overlap: 29, gap: 35 } : { overlap: 12, gap: 6 };
  }
  function selectedNetworks(source) {
    const view = presentation(source);
    return networks.filter((network, index) => view[['interactionsTikTok', 'interactionsTwitch', 'interactionsYouTube'][index]]);
  }
  function localImage(value, kind = 'effect') {
    const path = String(value || '');
    const pattern = kind === 'gift'
      ? /^\/assets\/creator-tools\/gifts\/images\/[a-z0-9_-]+\.(?:png|webp|jpg|jpeg)$/i
      : /^\/assets\/creator-tools\/(?:interactions|modifiers)\/[a-z0-9_-]+\.(?:png|webp|jpg|jpeg)$/i;
    return pattern.test(path) ? path : '';
  }
  function giftMap(catalog, communityGift) {
    const map = new Map();
    for (const gift of Array.isArray(catalog?.gifts) ? catalog.gifts : []) {
      const path = localImage(gift?.imagePath, 'gift');
      if (path && /^\d+$/.test(String(gift?.giftId))) map.set(String(gift.giftId), { name: String(gift.name || ''), image: path });
    }
    if (communityGift && /^\d+$/.test(String(communityGift.giftId))) {
      const image = localImage(communityGift.imagePath, 'gift') || localImage(communityGift.placeholderImagePath, 'gift');
      if (image) map.set(String(communityGift.giftId), { name: String(communityGift.name || ''), image });
    }
    return map;
  }
  function items(state = {}, source = {}, catalog = {}, artwork = {}, locale = 'es') {
    const enabled = selectedNetworks(source);
    const gifts = giftMap(catalog, state.communityGift);
    const grouped = new Map();
    for (const rule of Array.isArray(state.rules) ? state.rules : []) {
      if (!rule || rule.enabled !== true || !enabled.includes(rule.platform) ||
          !triggers[rule.platform]?.includes(rule.eventType)) continue;
      const effect = artwork[rule.interaction];
      const image = localImage(effect?.imagePath);
      if (!image || !(Number(rule.every) > 0)) continue;
      const quantity = Math.round(number(rule.quantity, 1, 1, 50));
      const duration = Math.round(number(rule.durationSeconds, 0, 0, 3600));
      const countdown = Math.round(number(rule.countdownSeconds, 0, 0, 3600));
      const key = JSON.stringify([rule.interaction, quantity, duration, countdown]);
      const gift = rule.eventType === 'gift' ? gifts.get(String(rule.giftId)) : null;
      const offer = {
        platform: rule.platform,
        eventType: rule.eventType,
        every: number(rule.every, 1, 1, 1000000),
        image: gift?.image || '',
        giftName: gift?.name || String(rule.giftName || ''),
        rewardName: rule.eventType === 'redemption' ? String(rule.rewardName || '').slice(0, 64) : '',
      };
      // An unknown gift gets a clearly labelled generic gift, never an invented catalog image.
      if (!grouped.has(key)) grouped.set(key, {
        key, interaction: rule.interaction, quantity, duration, countdown, image,
        name: String(effect.names?.[locale] || effect.names?.es || rule.interaction),
        category: ['attack', 'challenge', 'help'].includes(effect.category) ? effect.category
          : rule.interaction.startsWith('help_') ? 'help' : rule.interaction.startsWith('challenge_') ? 'challenge' : 'attack',
        offers: [],
      });
      const item = grouped.get(key);
      const offerKey = JSON.stringify([offer.platform, offer.eventType, rule.giftId || '', offer.every, offer.rewardName]);
      if (!item.offers.some(entry => entry.key === offerKey)) item.offers.push({ ...offer, key: offerKey });
    }
    return Array.from(grouped.values());
  }
  function paymentSize(count, adaptive = true) {
    const size = !adaptive ? 34 : count <= 1 ? 62 : count === 2 ? 46 : count === 3 ? 34 : 28;
    return size;
  }
  function pages(items, pageSize = 9) {
    const result = [];
    for (let index = 0; index < items.length; index += pageSize) result.push(items.slice(index, index + pageSize));
    return result;
  }
  function nextPage(current, count) { return count > 0 ? (current + 1) % count : 0; }
  function offset(current, pixelsPerSecond, elapsedMs, cycleWidth) {
    return cycleWidth > 0 ? (current + pixelsPerSecond * Math.max(0, elapsedMs) / 1000) % cycleWidth : 0;
  }
  function repetitions(cycleWidth, viewportWidth) {
    return cycleWidth > 0 ? Math.max(2, Math.ceil(viewportWidth / cycleWidth) + 1) : 2;
  }
  function opaqueBounds(width, height, pixels) {
    let left = width, top = height, right = -1, bottom = -1;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      if (pixels[(y * width + x) * 4 + 3] <= 24) continue;
      left = Math.min(left, x); top = Math.min(top, y); right = Math.max(right, x); bottom = Math.max(bottom, y);
    }
    if (right < left || bottom < top) return { ratio: 1, width: 1, height: 1, left: 0, top: 0 };
    const visibleHeight = bottom - top + 1;
    return { ratio: (right - left + 1) / visibleHeight, width: width / visibleHeight,
      height: height / visibleHeight, left: -left / visibleHeight, top: -top / visibleHeight };
  }
  function containArtwork(bounds) {
    const scale = 1 / Math.max(1, bounds.ratio);
    return { cropWidth: bounds.ratio * scale, cropHeight: scale,
      width: bounds.width * scale, height: bounds.height * scale,
      left: bounds.left * scale, top: bounds.top * scale };
  }
  host.CreatorToolsInteractionsOverlayModel = Object.freeze({ presentation, selectedNetworks, localImage, items,
    paymentSize, pages, nextPage, offset, repetitions, opaqueBounds, containArtwork, floatingLayout });
})(typeof window === 'undefined' ? globalThis : window);
