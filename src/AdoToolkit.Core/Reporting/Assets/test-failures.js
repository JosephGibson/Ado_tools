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
  let copyTimer;
  let filterTimer;
  // Search text is the whole element, collapsed parts included, lowercased once on first use.
  const text = node => {
    let value = texts.get(node);
    if (value === undefined) { value = node.textContent.toLocaleLowerCase(lang); texts.set(node, value); }
    return value;
  };
  // Every term must match. A Test Case ID matches with or without '#'; other terms match text.
  const matches = (node, terms, testCase) => terms.every(term =>
    (/^#?\d+$/.test(term) && testCase === term.replace('#', '')) || text(node).includes(term));
  const activeView = () => views.find(view => view.classList.contains('is-active'));
  const show = id => {
    const target = views.find(view => view.id === id) || views[0];
    views.forEach(view => view.classList.toggle('is-active', view === target));
    all('[data-view-link]').forEach(link => {
      if (link.dataset.viewLink === target.id) link.setAttribute('aria-current', 'page');
      else link.removeAttribute('aria-current');
    });
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
  const focus = card => {
    if (!card) return;
    select(card);
    card.focus({ preventScroll: true });
    card.scrollIntoView({ block: 'start' });
  };
  const apply = () => {
    const terms = filter.value.toLocaleLowerCase(lang).split(/\s+/).filter(Boolean);
    const group = failing ? failing.value : '';
    cards.forEach(card => {
      const failed = (card.dataset.failing || '').split(' ').filter(Boolean);
      const hidden = !matches(card, terms, card.dataset.testCase)
        || (group.startsWith('g') && !failed.includes(group.slice(1)))
        || (group.startsWith('o') && !(failed.length === 1 && failed[0] === group.slice(1)))
        || toggles.some(toggle => toggle.dataset.toggle === 'attachments' ? toggle.checked && +card.dataset.attachmentCount === 0 :
          !toggle.checked && toggle.dataset.toggle === card.dataset.classification);
      card.hidden = hidden;
      rows.get(card).forEach(row => { row.hidden = hidden; });
      // Mark the attempts and groups that hold the search terms; nothing opens by itself.
      attempts.get(card).forEach(attempt => attempt.classList.toggle('is-match', terms.length > 0 && !hidden && matches(attempt, terms)));
      all('.attempt-group', card).forEach(node => node.classList.toggle('is-match', node.querySelector('.attempt.is-match') !== null));
    });
    all('.error-cluster').forEach(cluster => { cluster.hidden = all('tr[data-index-for]', cluster).every(row => row.hidden); });
    updateCount();
    if (current?.hidden) select(null);
  };
  const fragment = () => {
    let target = null;
    try { target = document.getElementById(decodeURIComponent(location.hash.slice(1))); } catch { target = null; }
    const view = target?.closest('[data-view]');
    show(view ? view.id : 'overview');
    if (!target || target === view) { window.scrollTo(0, 0); return; }
    const card = target.closest('.failure-card');
    if (card) {
      card.hidden = false;
      rows.get(card).forEach(row => { row.hidden = false; });
      select(card);
      updateCount();
    }
    for (let node = target; node; node = node.parentElement) if (node.matches('details')) node.open = true;
    target.scrollIntoView({ block: 'start' });
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
  filter.addEventListener('input', () => { clearTimeout(filterTimer); filterTimer = setTimeout(apply, 150); });
  failing?.addEventListener('change', apply);
  toggles.forEach(toggle => toggle.addEventListener('change', apply));
  all('[data-action]').forEach(button => button.addEventListener('click', () => {
    const action = button.dataset.action;
    if (action === 'copy') { void copy(button); return; }
    if (action === 'expand' || action === 'collapse') {
      if (activeView()?.id !== 'details') location.hash = 'details';
      cards.filter(card => !card.hidden).forEach(card => all('.attempt-group, .attempt', card).forEach(node => { node.open = action === 'expand'; }));
      return;
    }
    const scope = button.closest('.code-section');
    const active = button.getAttribute('aria-pressed') !== 'true';
    button.setAttribute('aria-pressed', String(active));
    scope.classList.toggle(action === 'wrap' ? 'wrap-code' : 'hide-framework', active);
  }));
  document.addEventListener('focusin', event => {
    const card = event.target.closest('.failure-card');
    if (card) select(card);
  });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && event.target === filter) {
      filter.value = '';
      if (failing) failing.value = '';
      toggles.forEach(toggle => { toggle.checked = toggle.dataset.toggle !== 'attachments'; });
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
      const position = items.findIndex(item => cardOf(item) === current);
      const next = items[Math.max(0, Math.min(items.length - 1, position + (event.key === 'j' ? 1 : -1)))];
      if (!next) return;
      event.preventDefault();
      if (details) { focus(next); return; }
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
        row.closest('details').open = true;
        row.scrollIntoView({ block: 'center' });
      }
    }
  });
  window.addEventListener('hashchange', fragment);
  all('[data-enhance]').forEach(control => { control.hidden = false; });
  root.classList.add('views');
  const header = document.querySelector('.top-bar');
  // Table headers stick below the top bar; scroll targets keep a little more room.
  const measure = () => {
    const height = Math.ceil(header.getBoundingClientRect().height);
    root.style.setProperty('--report-header-height', `${height}px`);
    root.style.setProperty('--report-header-offset', `${height + 16}px`);
  };
  measure();
  if (typeof ResizeObserver === 'function') new ResizeObserver(measure).observe(header);
  apply();
  fragment();
})();
