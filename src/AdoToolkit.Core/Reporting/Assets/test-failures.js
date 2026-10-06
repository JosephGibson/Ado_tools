(() => {
  'use strict';
  const root = document.documentElement;
  const lang = root.lang;
  const all = (selector, scope = document) => [...scope.querySelectorAll(selector)];
  const cards = all('.failure-card');
  const views = all('[data-view]');
  const filter = document.querySelector('[data-filter]');
  const failing = document.querySelector('[data-failing]');
  const toggles = all('[data-toggle]');
  const status = document.querySelector('[data-copy-status]');
  const count = document.querySelector('[data-filter-count]');
  const numbers = new Intl.NumberFormat(lang);
  const rows = new Map(cards.map(card => [card, all(`[data-index-for="${card.id}"]`)]));
  const attempts = new Map(cards.map(card => [card, all('.attempt', card)]));
  const texts = new WeakMap();
  let current = null;
  // The table row last reached by keyboard or focus: a test listed under two bugs has two rows in one view.
  let currentRow = null;
  let copyTimer;
  let filterTimer;
  // Search ignores case and accents: "echec" finds « Échec ».
  const fold = value => value.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase(lang);
  // Search text is the whole element, collapsed parts included, folded once on first use.
  const text = node => {
    let value = texts.get(node);
    if (value === undefined) { value = fold(node.textContent); texts.set(node, value); }
    return value;
  };
  // Every term must match. A Test Case ID matches with or without '#'; other terms match text.
  const matches = (node, terms, testCase) => terms.every(term =>
    (/^#?\d+$/.test(term) && testCase === term.replace('#', '')) || text(node).includes(term));
  const activeView = () => views.find(view => view.classList.contains('is-active'));
  const header = document.querySelector('.top-bar');
  // Sticky table headers and the page's scroll padding follow the top bar's height. True when it changed.
  let bandHeight = 0;
  const measure = () => {
    const height = Math.ceil(header.getBoundingClientRect().height);
    if (height === bandHeight) return false;
    bandHeight = height;
    root.style.setProperty('--report-header-height', `${height}px`);
    return true;
  };
  // Scrolls a node to the top, clear of the band. The scroll lays out the cards that content-visibility
  // skipped; a page that grows a scrollbar can rewrap the band, so the scroll is repeated at its new height.
  const reveal = node => {
    node.scrollIntoView({ block: 'start' });
    if (measure()) node.scrollIntoView({ block: 'start' });
  };
  const show = id => {
    const target = views.find(view => view.id === id) || views[0];
    views.forEach(view => view.classList.toggle('is-active', view === target));
    all('[data-view-link]').forEach(link => {
      if (link.dataset.viewLink === target.id) link.setAttribute('aria-current', 'page');
      else link.removeAttribute('aria-current');
    });
    // Expand all and Collapse all show in Details alone. They can change the band's height, which
    // a scroll right after this needs at once, before the ResizeObserver reports it.
    all('[data-details-only]').forEach(control => { control.hidden = target.id !== 'details'; });
    measure();
  };
  const select = card => {
    current = card;
    cards.forEach(candidate => {
      const on = candidate === card;
      candidate.classList.toggle('is-current', on);
      rows.get(candidate).forEach(row => row.classList.toggle('is-current', on));
    });
  };
  const updateCount = () => {
    const shown = cards.filter(card => !card.hidden).length;
    count.textContent = count.dataset.labelCount.replace('{0}', numbers.format(shown)).replace('{1}', numbers.format(cards.length));
    all('[data-no-matches]').forEach(note => { note.hidden = shown !== 0; });
  };
  // Reaching a test opens the attempt of its latest error.
  const arrive = card => {
    for (let node = document.getElementById(card.dataset.latestError); node && node !== card; node = node.parentElement) if (node.matches('details')) node.open = true;
  };
  const focus = card => {
    if (!card) return;
    select(card);
    arrive(card);
    card.focus({ preventScroll: true });
    reveal(card);
  };
  // A count chip is a filter shortcut: that kind alone, or the tests with attachments.
  const toggle = name => toggles.find(item => item.dataset.toggle === name);
  const other = kind => toggle(kind === 'failed' ? 'flaky' : 'failed');
  const chips = all('.count-chip:not([data-zero])').map(chip => [chip, ['failed', 'flaky', 'attachments'].find(kind => chip.classList.contains('status-' + kind))])
    .filter(([, kind]) => kind && toggle(kind));
  const pressed = kind => toggle(kind).checked && (kind === 'attachments' || !other(kind).checked);
  const apply = () => {
    const terms = fold(filter.value).split(/\s+/).filter(Boolean);
    const group = failing ? failing.value : '';
    cards.forEach(card => {
      const failed = (card.dataset.failing || '').split(' ').filter(Boolean);
      const hidden = !matches(card, terms, card.dataset.testCase)
        || (group.startsWith('g') && !failed.includes(group.slice(1)))
        || (group.startsWith('o') && !(failed.length === 1 && failed[0] === group.slice(1)))
        || toggles.some(toggle => toggle.dataset.toggle === 'attachments' ? toggle.checked && +card.dataset.attachmentCount === 0 :
          toggle.dataset.toggle === 'untracked' ? toggle.checked && card.dataset.openBug !== undefined :
          !toggle.checked && toggle.dataset.toggle === card.dataset.classification);
      card.hidden = hidden;
      rows.get(card).forEach(row => { row.hidden = hidden; });
      // Mark the attempts and groups that hold the search terms; nothing opens by itself.
      attempts.get(card).forEach(attempt => attempt.classList.toggle('is-match', terms.length > 0 && !hidden && matches(attempt, terms)));
      all('.attempt-group', card).forEach(node => node.classList.toggle('is-match', node.querySelector('.attempt.is-match') !== null));
    });
    all('.error-cluster').forEach(cluster => { cluster.hidden = all('tr[data-index-for]', cluster).every(row => row.hidden); });
    all('.cluster-section').forEach(section => { section.hidden = all('.error-cluster[data-generic]').every(cluster => cluster.hidden); });
    chips.forEach(([chip, kind]) => chip.setAttribute('aria-pressed', String(pressed(kind))));
    updateCount();
    if (current?.hidden) select(null);
  };
  const press = kind => {
    const on = !pressed(kind);
    toggle(kind).checked = on || kind !== 'attachments';
    if (kind !== 'attachments') other(kind).checked = !on;
    apply();
  };
  const fragment = () => {
    let target = null;
    try { target = document.getElementById(decodeURIComponent(location.hash.slice(1))); } catch { target = null; }
    const view = target?.closest('[data-view]');
    show(view ? view.id : 'overview');
    if (!target || target === view) {
      // Back to a table of tests returns to the current test's row, not to the top.
      const shown = activeView();
      const candidates = current && ['overview', 'by-error', 'bugs'].includes(shown.id)
        ? all('tr[data-index-for]', shown).filter(item => !item.hidden && item.dataset.indexFor === current.id) : [];
      const row = candidates.includes(currentRow) ? currentRow : candidates[0];
      if (!row) { window.scrollTo(0, 0); return; }
      currentRow = row;
      row.scrollIntoView({ block: 'center' });
      row.querySelector('.col-test a').focus({ preventScroll: true });
      return;
    }
    const card = target.closest('.failure-card');
    if (card) {
      card.hidden = false;
      rows.get(card).forEach(row => { row.hidden = false; });
      select(card);
      updateCount();
      if (target === card) arrive(card);
    }
    for (let node = target; node; node = node.parentElement) if (node.matches('details')) node.open = true;
    reveal(target);
  };
  const copy = async button => {
    const scope = button.closest('.code-section, .name-section');
    const target = scope.querySelector('[data-copy-value], pre code');
    try {
      await navigator.clipboard.writeText(target.textContent);
      status.textContent = status.dataset.labelDone;
    } catch {
      const range = document.createRange();
      range.selectNodeContents(target);
      const selection = window.getSelection();
      selection.removeAllRanges();
      selection.addRange(range);
      status.textContent = status.dataset.labelSelected;
    }
    status.hidden = false;
    clearTimeout(copyTimer);
    copyTimer = setTimeout(() => { status.hidden = true; }, 5000);
  };
  // Wrap and Framework buttons, pressed or not.
  const set = (button, active) => {
    button.setAttribute('aria-pressed', String(active));
    button.closest('.code-section').classList.toggle(button.dataset.action === 'wrap' ? 'wrap-code' : 'hide-framework', active);
  };
  filter.addEventListener('input', () => { clearTimeout(filterTimer); filterTimer = setTimeout(apply, 150); });
  failing?.addEventListener('change', apply);
  toggles.forEach(toggle => toggle.addEventListener('change', apply));
  chips.forEach(([chip, kind]) => {
    chip.setAttribute('role', 'button');
    chip.tabIndex = 0;
    chip.addEventListener('click', () => press(kind));
    chip.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); press(kind); } });
  });
  all('[data-action]').forEach(button => button.addEventListener('click', () => {
    const action = button.dataset.action;
    if (action === 'copy') { void copy(button); return; }
    if (action === 'expand' || action === 'collapse') {
      cards.filter(card => !card.hidden).forEach(card => all('.attempt-group, .attempt', card).forEach(node => { node.open = action === 'expand'; }));
      return;
    }
    set(button, button.getAttribute('aria-pressed') !== 'true');
  }));
  document.addEventListener('focusin', event => {
    const card = event.target.closest('.failure-card');
    if (card) select(card);
    const row = event.target.closest('tr[data-index-for]');
    if (row) currentRow = row;
  });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && event.target === filter) {
      filter.value = '';
      if (failing) failing.value = '';
      toggles.forEach(toggle => { toggle.checked = toggle.defaultChecked; });
      apply();
    }
    if (event.ctrlKey || event.metaKey || event.altKey ||
        event.target.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"])')) return;
    if (event.key === '/') { event.preventDefault(); filter.focus(); return; }
    const view = activeView();
    const details = view?.id === 'details';
    if (event.key === 'j' || event.key === 'k') {
      // Details moves between cards; the tables move between their visible rows.
      const items = details ? cards.filter(card => !card.hidden) : all('tr[data-index-for]', view).filter(row => !row.hidden);
      const cardOf = item => details ? item : document.getElementById(item.dataset.indexFor);
      // In a table, continue from the row itself while it is a row of the current test; otherwise
      // from the first row of the current test.
      const known = details || currentRow?.dataset.indexFor !== current?.id ? -1 : items.indexOf(currentRow);
      const position = known >= 0 ? known : items.findIndex(item => cardOf(item) === current);
      const next = items[Math.max(0, Math.min(items.length - 1, position + (event.key === 'j' ? 1 : -1)))];
      if (!next) return;
      event.preventDefault();
      if (details) { focus(next); return; }
      currentRow = next;
      select(cardOf(next));
      next.querySelector('.col-test a').focus();
    }
    if (event.key === 'o' && details) {
      const card = current || cards.find(candidate => !candidate.hidden);
      const node = event.target.closest('.attempt, .attempt-group') || card?.querySelector('.attempt-group, .attempt');
      if (node) { event.preventDefault(); node.open = !node.open; }
    }
  });
  document.addEventListener('click', event => {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
    const link = event.target.closest('a');
    if (!link) return;
    if (link.getAttribute('href')?.startsWith('#') && link.hash === location.hash) fragment();
    if (link.matches('.history-chart a, .history-cell')) {
      const card = link.closest('.failure-card');
      if (card && link.getAttribute('aria-current') === 'true') { event.preventDefault(); focus(card); return; }
      const row = document.querySelector(`.history-data tr[data-build-id="${link.dataset.buildId}"]`);
      if (row) {
        event.preventDefault();
        show('runs');
        row.scrollIntoView({ block: 'center' });
      }
    }
  });
  window.addEventListener('hashchange', fragment);
  all('[data-enhance]').forEach(control => { control.hidden = false; });
  // A trace opens on the test's own code and a message wrapped; copy and print keep every frame.
  all('.code-section').forEach(section => {
    const button = section.querySelector(section.querySelector('code.lang-error') ? '[data-action="wrap"]'
      : section.querySelector('.first-user-frame') ? '[data-action="framework"]' : null);
    if (button) set(button, true);
  });
  root.classList.add('views');
  measure();
  if (typeof ResizeObserver === 'function') new ResizeObserver(measure).observe(header);
  apply();
  fragment();
})();
