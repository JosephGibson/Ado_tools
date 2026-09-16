(() => {
  'use strict';
  const all = (selector, root = document) => [...root.querySelectorAll(selector)];
  const cards = all('.failure-card');
  const filter = document.querySelector('[data-filter]');
  const toggles = all('[data-toggle]');
  const status = document.querySelector('[data-copy-status]');
  const dialog = document.querySelector('dialog');
  const count = document.querySelector('[data-filter-count]');
  const numbers = new Intl.NumberFormat(document.documentElement.lang);
  let current = null;
  let printState = null;
  let copyTimer;
  const visible = () => cards.filter(card => !card.hidden);
  const index = card => document.querySelector(`[data-index-for="${card.id}"]`);
  const select = card => {
    current = card;
    cards.forEach(candidate => {
      candidate.classList.toggle('is-current', candidate === card);
      index(candidate).classList.toggle('is-current', candidate === card);
    });
  };
  const updateCount = () => {
    const shown = visible().length;
    count.textContent = count.dataset.labelCount.replace('{0}', numbers.format(shown)).replace('{1}', numbers.format(cards.length));
    document.querySelector('[data-no-matches]').hidden = shown !== 0;
  };
  const focus = card => {
    if (!card) return;
    select(card);
    card.focus({ preventScroll: true });
    card.scrollIntoView({ block: 'start' });
  };
  const apply = () => {
    const query = filter.value.toLocaleLowerCase(document.documentElement.lang);
    cards.forEach(card => {
      const names = all('[data-short-name], [data-full-name]', card).map(node => node.textContent).join('\n');
      card.hidden = !names.toLocaleLowerCase(document.documentElement.lang).includes(query) || toggles.some(toggle =>
        toggle.dataset.toggle === 'attachments' ? toggle.checked && +card.dataset.attachmentCount === 0 :
          !toggle.checked && toggle.dataset.toggle === card.dataset.classification);
      index(card).hidden = card.hidden;
    });
    updateCount();
    if (current?.hidden) select(null);
  };
  const fragment = () => {
    let target;
    try { target = document.getElementById(decodeURIComponent(location.hash.slice(1))); } catch { return; }
    const card = target?.closest('.failure-card');
    if (!card) return;
    card.hidden = false;
    index(card).hidden = false;
    for (let node = target; node; node = node.parentElement) if (node.matches('details')) node.open = true;
    select(card);
    updateCount();
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
  filter.addEventListener('input', apply);
  toggles.forEach(toggle => toggle.addEventListener('change', apply));
  all('[data-action]').forEach(button => button.addEventListener('click', () => {
    const action = button.dataset.action;
    if (action === 'copy') { void copy(button); return; }
    if (action === 'close') { dialog.close(); return; }
    if (action === 'expand' || action === 'collapse') {
      all('.attempt').forEach(attempt => { attempt.open = action === 'expand'; });
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
      toggles.forEach(toggle => { toggle.checked = toggle.dataset.toggle !== 'attachments'; });
      apply();
    }
    if (event.ctrlKey || event.metaKey || event.altKey || dialog.open ||
        event.target.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"])')) return;
    if (event.key === '/') { event.preventDefault(); filter.focus(); return; }
    if (event.key === 'j' || event.key === 'k') {
      event.preventDefault();
      const list = visible();
      const position = list.indexOf(current);
      focus(list[Math.max(0, Math.min(list.length - 1, position + (event.key === 'j' ? 1 : -1)))]);
    }
    if (event.key === 'o') {
      const card = current || visible()[0];
      const attempt = event.target.closest('.attempt') || all('.attempt', card || document).at(-1);
      if (attempt) { event.preventDefault(); attempt.open = !attempt.open; }
    }
  });
  document.addEventListener('click', event => {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
    const link = event.target.closest('a');
    if (!link) return;
    if (link.getAttribute('href')?.startsWith('#f-') && link.hash === location.hash) fragment();
    const image = link.querySelector('img');
    if (image && link.closest('.thumbnail-grid') && image.complete && image.naturalWidth && typeof dialog.showModal === 'function') {
      event.preventDefault();
      dialog.querySelector('[data-image-slot]').replaceChildren(image.cloneNode(true));
      dialog.showModal();
      return;
    }
    if (link.matches('.history-chart a, .history-cell')) {
      const card = link.closest('.failure-card');
      if (card && link.getAttribute('aria-current') === 'true') { event.preventDefault(); focus(card); return; }
      const row = document.querySelector(`.history-data tr[data-build-id="${link.dataset.buildId}"]`);
      if (row) {
        event.preventDefault();
        row.closest('details').open = true;
        row.scrollIntoView({ block: 'center' });
      }
    }
  });
  window.addEventListener('hashchange', fragment);
  window.addEventListener('beforeprint', () => {
    if (!printState) printState = cards.map(card => [card, card.hidden]);
    cards.forEach(card => { card.hidden = false; index(card).hidden = false; });
    all('.attempt[data-failure-class="true"]').forEach(attempt => { attempt.open = true; });
  });
  window.addEventListener('afterprint', () => {
    printState?.forEach(([card, hidden]) => { card.hidden = hidden; index(card).hidden = hidden; });
    printState = null;
  });
  all('[data-enhance]').forEach(control => { control.hidden = false; });
  const header = document.querySelector('.top-bar');
  const measure = () => document.documentElement.style.setProperty('--report-header-offset', `${Math.ceil(header.getBoundingClientRect().height) + 16}px`);
  measure();
  if (typeof ResizeObserver === 'function') new ResizeObserver(measure).observe(header);
  apply();
  fragment();
})();
