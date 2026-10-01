(() => {
  'use strict';
  const root = document.documentElement;
  const lang = root.lang;
  const all = (selector, scope = document) => [...scope.querySelectorAll(selector)];
  const cases = all('.test-case');
  const multiple = cases.length > 1;
  const steps = all('.step-card:not(.shared-banner)');
  // A document moves between its cases and filters them; one case moves between its steps.
  const items = multiple ? cases : steps;
  const filter = document.querySelector('[data-filter]');
  const toggles = all('[data-toggle]');
  const status = document.querySelector('[data-copy-status]');
  const count = document.querySelector('[data-filter-count]');
  const numbers = new Intl.NumberFormat(lang);
  const rows = new Map(cases.map(article => [article, article.id ? all(`[data-index-for="${article.id}"]`) : []]));
  const tokens = new Map(all('.param').map(node => [node, node.textContent]));
  let texts = new WeakMap();
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
  const matches = (node, terms, id) => terms.every(term =>
    (/^#?\d+$/.test(term) && id === term.replace('#', '')) || text(node).includes(term));
  const toggleOf = owner => owner.querySelector(owner.matches('.test-case') ? ':scope > header [data-action="toggle"]' : ':scope > [data-action="toggle"]');
  const collapse = (owner, collapsed) => {
    const button = toggleOf(owner);
    if (!button) return;
    owner.classList.toggle('is-collapsed', collapsed);
    button.setAttribute('aria-expanded', String(!collapsed));
  };
  const select = item => {
    current = item;
    items.forEach(candidate => {
      const on = candidate === item;
      candidate.classList.toggle('is-current', on);
      (rows.get(candidate) || []).forEach(row => row.classList.toggle('is-current', on));
    });
  };
  const updateCount = () => {
    const shown = items.filter(item => !item.hidden).length;
    count.textContent = count.dataset.labelCount.replace('{0}', numbers.format(shown)).replace('{1}', numbers.format(items.length));
    all('[data-no-matches]').forEach(note => { note.hidden = shown !== 0 || items.length === 0; });
  };
  const apply = () => {
    const terms = filter.value.toLocaleLowerCase(lang).split(/\s+/).filter(Boolean);
    if (multiple) {
      cases.forEach(article => {
        const data = article.querySelector(':scope > header').dataset;
        const hidden = !matches(article, terms, data.case)
          || toggles.some(toggle => toggle.checked && (toggle.dataset.toggle === 'partial' ? data.status !== 'partial' : data.failed === undefined));
        article.hidden = hidden;
        rows.get(article).forEach(row => { row.hidden = hidden; });
      });
      all('.toc-group').forEach(group => { group.hidden = all('tr[data-index-for]', group).every(row => row.hidden); });
    }
    // One case hides the steps that do not match; a document marks the steps that do.
    steps.forEach(card => {
      const match = terms.length > 0 && matches(card, terms);
      card.classList.toggle('is-match', multiple && match);
      if (!multiple) card.hidden = terms.length > 0 && !match;
    });
    if (!multiple) all('.shared-banner').forEach(banner => {
      const group = banner.nextElementSibling;
      banner.hidden = terms.length > 0 && !!group && group.matches('.shared-group')
        && all('.step-card:not(.shared-banner)', group).every(card => card.hidden);
    });
    updateCount();
    if (current?.hidden) select(null);
  };
  const fragment = () => {
    let target = null;
    try { target = document.getElementById(decodeURIComponent(location.hash.slice(1))); } catch { target = null; }
    all('.section-links a').forEach(link => {
      if (target && link.hash === location.hash) link.setAttribute('aria-current', 'true');
      else link.removeAttribute('aria-current');
    });
    if (!target) return;
    // The target is shown even when a filter hides it or a group is closed around it.
    for (let node = target; node; node = node.parentElement) {
      if (node.matches('.test-case, .step-card')) node.hidden = false;
      if (node.matches('.test-case')) { rows.get(node).forEach(row => { row.hidden = false; }); collapse(node, false); }
      if (node.matches('.shared-group') && node.previousElementSibling) { node.previousElementSibling.hidden = false; collapse(node.previousElementSibling, false); }
    }
    const item = target.closest(multiple ? '.test-case' : '.step-card:not(.shared-banner)');
    if (item) select(item);
    updateCount();
    target.scrollIntoView({ block: 'start' });
  };
  const copy = async button => {
    const target = button.closest('.name-section, .test-case > header').querySelector('[data-copy-value]');
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
  toggles.forEach(toggle => toggle.addEventListener('change', apply));
  all('[data-action]').forEach(button => button.addEventListener('click', () => {
    const action = button.dataset.action;
    if (action === 'copy') { void copy(button); return; }
    if (action === 'toggle') {
      const owner = button.closest('.shared-banner, .test-case');
      collapse(owner, !owner.classList.contains('is-collapsed'));
      return;
    }
    // Collapse closes the cases of a document, or the Shared Steps groups of one case; expand opens everything.
    const closing = action === 'collapse';
    (closing && multiple ? cases : closing ? all('.shared-banner') : [...cases, ...all('.shared-banner')]).forEach(owner => collapse(owner, closing));
  }));
  // The steps show the values of the chosen iteration in place of the parameter names.
  all('[data-iteration]').forEach(choice => choice.addEventListener('change', () => {
    const article = choice.closest('.test-case');
    const row = choice.value === '' ? null : all('[data-parameters] > tr', article)[+choice.value];
    all('.param', article).forEach(node => {
      const cell = row ? row.cells[+node.dataset.param] : null;
      node.textContent = cell ? cell.textContent : tokens.get(node);
      node.classList.toggle('is-value', !!cell);
    });
    texts = new WeakMap();
    apply();
  }));
  document.addEventListener('focusin', event => {
    const item = event.target.closest?.(multiple ? '.test-case' : '.step-card:not(.shared-banner)');
    if (item) select(item);
  });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && event.target === filter) {
      filter.value = '';
      toggles.forEach(toggle => { toggle.checked = toggle.defaultChecked; });
      apply();
    }
    if (event.ctrlKey || event.metaKey || event.altKey ||
        event.target.closest?.('input, textarea, select, [contenteditable]:not([contenteditable="false"])')) return;
    if (event.key === '/') { event.preventDefault(); filter.focus(); return; }
    if (event.key !== 'j' && event.key !== 'k') return;
    // Items inside a closed group have no box and are skipped.
    const visible = items.filter(item => !item.hidden && item.getClientRects().length > 0);
    const next = visible[Math.max(0, Math.min(visible.length - 1, visible.indexOf(current) + (event.key === 'j' ? 1 : -1)))];
    if (!next) return;
    event.preventDefault();
    select(next);
    next.focus({ preventScroll: true });
    next.scrollIntoView({ block: 'start' });
  });
  document.addEventListener('click', event => {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
    const link = event.target.closest?.('a');
    if (link && link.getAttribute('href')?.startsWith('#') && link.hash === location.hash) fragment();
  });
  window.addEventListener('hashchange', fragment);
  all('[data-enhance]').forEach(control => { control.hidden = false; });
  items.forEach(item => { item.tabIndex = -1; });
  const header = document.querySelector('.top-bar');
  // Scroll targets stop below the sticky top bar.
  const measure = () => root.style.setProperty('--report-header-offset', `${Math.ceil(header.getBoundingClientRect().height) + 16}px`);
  measure();
  if (typeof ResizeObserver === 'function') new ResizeObserver(measure).observe(header);
  apply();
  fragment();
})();
