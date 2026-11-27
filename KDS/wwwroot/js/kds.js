// Browser-side helpers for the Blazor app:
// screen settings (localStorage), masonry layout, remote control and the chime.

(function () {
  const STORAGE_KEY = 'kds-screen-settings-v1';

  // remotes / clickers send PageUp/PageDown, and b or . for the black screen button
  const DEFAULT_KEYS = {
    left:   ['ArrowLeft'],
    right:  ['ArrowRight'],
    up:     ['ArrowUp'],
    down:   ['ArrowDown'],
    select: ['Enter', ' '],
    bump:   ['b', '.'],
    next:   ['PageDown'],
    prev:   ['PageUp'],
    recall: ['r'],
    clear:  ['Escape', 'Backspace', 'GoBack', 'BrowserBack'],
  };

  const DEFAULT_SETTINGS = {
    textScale: 1,
    highContrast: false,
    statusLabels: false,
    reduceMotion: false,
    darkBoard: false,
    warnAfterMin: 5,
    lateAfterMin: 10,
    sound: false,
    remoteEnabled: true,
    remoteHints: true,
    sidebarCollapsed: false,
    remoteKeys: DEFAULT_KEYS,
  };

  let current = null;

  // settings

  function loadSettings() {
    let saved = {};
    try {
      saved = JSON.parse(localStorage.getItem(STORAGE_KEY)) || {};
    } catch (e) {
      // private mode etc.
    }
    current = {
      ...DEFAULT_SETTINGS,
      ...saved,
      remoteKeys: { ...DEFAULT_KEYS, ...(saved.remoteKeys || {}) },
    };
    return current;
  }

  function saveSettings(settings) {
    current = settings;
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
    } catch (e) {
      // ignore
    }
    applySettings(settings);
  }

  function resetSettings() {
    try { localStorage.removeItem(STORAGE_KEY); } catch (e) { /* ignore */ }
    const fresh = loadSettings();
    applySettings(fresh);
    return fresh;
  }

  function applySettings(settings) {
    document.documentElement.style.setProperty('--text-scale', settings.textScale);
    document.body.classList.toggle('a11y-contrast', !!settings.highContrast);
    document.body.classList.toggle('a11y-reduce-motion', !!settings.reduceMotion);
    document.body.classList.toggle('theme-dark', !!settings.darkBoard);
    if (activeBoard) scheduleLayout();
  }

  function defaultRemoteKeys() {
    return JSON.parse(JSON.stringify(DEFAULT_KEYS));
  }

  function timeZone() {
    try {
      return Intl.DateTimeFormat().resolvedOptions().timeZone || '';
    } catch (e) {
      return '';
    }
  }

  function isNarrow() {
    return window.matchMedia('(max-width: 899px)').matches;
  }

  // chime

  let audioCtx = null;



  // KDS ORDER SOUND:
    const customSound = new Audio('lib/order-sound.mp3'); 
    customSound.volume = 1.0; 

    // javascript function to make it beep:
  function beep() {
      try {

          customSound.currentTime = 0; 
          customSound.play(); 


      
    } catch (e) {
      // no audio
    }
  }

  // masonry board
  // Blazor renders the cards oldest first; we only set their position,
  // so Blazor still owns the DOM.

  const MIN_COLUMN_WIDTH = 300;
  const COLUMN_GAP = 20;

  let activeBoard = null;
  let columns = [];
  let frame = 0;
  let mutationObserver = null;
  let resizeObserver = null;

  function attachBoard(board) {
    detachBoard();
    activeBoard = board;
    mutationObserver = new MutationObserver(scheduleLayout);
    mutationObserver.observe(board, {
      childList: true,
      subtree: true,
      characterData: true,
      attributes: true,
      attributeFilter: ['class'],
    });
    resizeObserver = new ResizeObserver(scheduleLayout);
    resizeObserver.observe(board);
    if (document.fonts) document.fonts.ready.then(scheduleLayout);
    layout();
  }

  function detachBoard() {
    if (mutationObserver) mutationObserver.disconnect();
    if (resizeObserver) resizeObserver.disconnect();
    mutationObserver = resizeObserver = null;
    activeBoard = null;
    columns = [];
    hideHint();
  }

  function scheduleLayout() {
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(layout);
  }

  function layout() {
    const board = activeBoard;
    if (!board || !board.isConnected) return;

    const cards = Array.from(board.querySelectorAll(':scope > .order-card'));
    const style = getComputedStyle(board);
    const padLeft = parseFloat(style.paddingLeft);
    const padTop = parseFloat(style.paddingTop);
    const padBottom = parseFloat(style.paddingBottom);
    const inner = board.clientWidth - padLeft - parseFloat(style.paddingRight);
    const scale = (current && Number(current.textScale)) || 1;

    const count = Math.max(1, Math.floor((inner + COLUMN_GAP) / (MIN_COLUMN_WIDTH * scale + COLUMN_GAP)));
    const width = (inner - COLUMN_GAP * (count - 1)) / count;

    if (!board.classList.contains('is-masonry')) board.classList.add('is-masonry');

    // set every width first so we only measure once
    cards.forEach((card) => { card.style.width = `${width}px`; });
    const heights = cards.map((card) => card.offsetHeight);

    const tops = new Array(count).fill(0);
    columns = Array.from({ length: count }, () => []);

    cards.forEach((card, i) => {
      let col = 0;
      for (let c = 1; c < count; c++) if (tops[c] < tops[col]) col = c;
      card.style.left = `${padLeft + col * (width + COLUMN_GAP)}px`;
      card.style.top = `${padTop + tops[col]}px`;
      tops[col] += heights[i] + COLUMN_GAP;
      columns[col].push(card);
    });

    const tallest = Math.max(0, ...tops.map((t) => t - COLUMN_GAP));
    board.style.height = cards.length ? `${padTop + tallest + padBottom}px` : '';

    remote.apply(false);
  }

  // remote control
  // cursor = { orderId, row }, row -1 = whole card, 0+ = item.
  // The selection is drawn with data-selected / data-cursor attributes,
  // which Blazor never touches, so they survive re-renders.

  const KEY_NAMES = {
    ArrowLeft: '◀', ArrowRight: '▶', ArrowUp: '▲', ArrowDown: '▼',
    ' ': 'Space', Enter: 'OK', Escape: 'Esc', PageUp: 'Page Up', PageDown: 'Page Down',
    GoBack: 'Back', BrowserBack: 'Back', Backspace: 'Backspace', '.': '. (period)',
  };

  // some remotes send key "Unidentified"
  function keyOf(event) {
    const key = event.key && event.key !== 'Unidentified' ? event.key : `code:${event.code || event.keyCode}`;
    return key.length === 1 ? key.toLowerCase() : key;
  }

  function keyName(key) {
    return KEY_NAMES[key] || (key.length === 1 ? key.toUpperCase() : key.replace(/^code:/, 'Code '));
  }

  const remote = (function () {
    let cursor = { orderId: null, row: -1 };
    let lastIndex = 0;
    let hintTimer = null;
    let selectRecalled = false;

    const cards = () => (activeBoard ? Array.from(activeBoard.querySelectorAll(':scope > .order-card')) : []);
    const cardEl = (id) => cards().find((c) => c.dataset.orderId === id) || null;
    const rowsOf = (card) => Array.from(card.querySelectorAll('.order-item-row'));
    const columnOf = (card) => columns.findIndex((col) => col.includes(card));

    function isActive() {
      return !!activeBoard && !!current && current.remoteEnabled && !learning;
    }

    function selectedCard() {
      return cursor.orderId ? cardEl(cursor.orderId) : null;
    }

    function cursorY() {
      const card = selectedCard();
      if (!card) return 0;
      const target = cursor.row >= 0 ? rowsOf(card)[cursor.row] : card;
      const r = (target || card).getBoundingClientRect();
      return cursor.row >= 0 ? r.top + r.height / 2 : r.top + 40;
    }

    function set(orderId, row) {
      cursor = { orderId, row };
      if (orderId) lastIndex = Math.max(0, cards().findIndex((c) => c.dataset.orderId === orderId));
      apply(true);
    }

    function selectFirst() {
      const first = cards()[0];
      if (first) set(first.dataset.orderId, -1);
    }

    function distanceY(card, y) {
      const r = card.getBoundingClientRect();
      if (y >= r.top && y <= r.bottom) return 0;
      return Math.min(Math.abs(y - r.top), Math.abs(y - r.bottom));
    }

    function moveSideways(direction) {
      const y = cursorY();
      let i = columnOf(selectedCard()) + direction;
      // skip empty columns
      while (i >= 0 && i < columns.length) {
        const candidates = columns[i];
        if (candidates.length) {
          const best = candidates.reduce((a, b) => (distanceY(a, y) <= distanceY(b, y) ? a : b));
          return set(best.dataset.orderId, -1);
        }
        i += direction;
      }
    }

    function neighbourInColumn(card, direction) {
      const col = columns[columnOf(card)] || [];
      return col[col.indexOf(card) + direction] || null;
    }

    function moveUp() {
      if (cursor.row > 0) return set(cursor.orderId, cursor.row - 1);
      if (cursor.row === 0) return set(cursor.orderId, -1);
      const above = neighbourInColumn(selectedCard(), -1);
      if (above) set(above.dataset.orderId, -1);
    }

    function moveDown() {
      const card = selectedCard();
      if (cursor.row < rowsOf(card).length - 1) return set(cursor.orderId, cursor.row + 1);
      const below = neighbourInColumn(card, 1);
      if (below) set(below.dataset.orderId, -1);
    }

    function step(direction) {
      const all = cards();
      const next = all[all.indexOf(selectedCard()) + direction];
      if (next) set(next.dataset.orderId, -1);
    }

    function bumpSelected() {
      const card = selectedCard();
      const all = cards();
      const i = all.indexOf(card);
      const next = all[i + 1] || all[i - 1];
      cursor = { orderId: next ? next.dataset.orderId : null, row: -1 };
      card.querySelector('.order-card-bump').click();
      apply(true);
    }

    function actionFor(key) {
      const keys = (current && current.remoteKeys) || DEFAULT_KEYS;
      return Object.keys(keys).find((id) => (keys[id] || []).includes(key));
    }

    function isTyping(target) {
      return target && target.closest && target.closest('input, select, textarea, [contenteditable]');
    }

    function onKeyDown(event) {
      if (!isActive() || isTyping(event.target)) return;

      const action = actionFor(keyOf(event));
      if (!action) return;
      event.preventDefault(); // otherwise Enter/Space clicks the focused button

      if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();

      if (action === 'recall') {
        const btn = document.getElementById('btn-recall');
        if (btn && !btn.disabled) {
          selectRecalled = true;
          btn.click();
        }
        return;
      }
      if (action === 'clear') {
        cursor = { orderId: null, row: -1 };
        return apply(false);
      }

      if (!selectedCard()) return selectFirst();

      switch (action) {
        case 'left':   moveSideways(-1); break;
        case 'right':  moveSideways(1); break;
        case 'up':     moveUp(); break;
        case 'down':   moveDown(); break;
        case 'next':   step(1); break;
        case 'prev':   step(-1); break;
        case 'bump':   bumpSelected(); break;
        case 'select':
          if (cursor.row < 0) bumpSelected();
          else rowsOf(selectedCard())[cursor.row].click();
          break;
      }
    }

    // space clicks on keyup
    function onKeyUp(event) {
      if (isActive() && actionFor(keyOf(event)) && !isTyping(event.target)) event.preventDefault();
    }

    // runs after every layout pass
    function apply(scroll) {
      if (!activeBoard) return;

      // a recalled order comes back with the newest data-added
      if (selectRecalled) {
        const newest = cards().reduce((a, b) => (!a || Number(b.dataset.added) > Number(a.dataset.added) ? b : a), null);
        if (newest && newest.dataset.orderId !== cursor.orderId && Number(newest.dataset.added) > Date.now() - 5000) {
          selectRecalled = false;
          cursor = { orderId: newest.dataset.orderId, row: -1 };
          scroll = true;
        }
      }

      activeBoard.querySelectorAll('[data-selected], [data-cursor]').forEach((el) => {
        el.removeAttribute('data-selected');
        el.removeAttribute('data-cursor');
      });

      // selected order is gone, pick a neighbour
      if (cursor.orderId && !cardEl(cursor.orderId)) {
        const all = cards();
        const fallback = all[Math.min(lastIndex, all.length - 1)];
        cursor = { orderId: fallback ? fallback.dataset.orderId : null, row: -1 };
      }

      const card = selectedCard();
      if (card) {
        const rows = rowsOf(card);
        if (cursor.row >= rows.length) cursor.row = rows.length - 1;
        card.setAttribute('data-selected', '');
        const target = cursor.row >= 0 ? rows[cursor.row] : card;
        target.setAttribute('data-cursor', '');
        if (scroll) target.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
        if (scroll) showHint();
      } else {
        hideHint();
      }
    }

    function showHint() {
      const hint = document.getElementById('remote-hint');
      if (!hint || !current || !current.remoteHints) return;
      const ok = keyName((current.remoteKeys.select || [])[0] || 'Enter');
      hint.textContent = cursor.row < 0
        ? `${ok}: bump order  ·  ▼ items  ·  ◀ ▶ other orders`
        : `${ok}: check / uncheck item  ·  ▲ ▼ items  ·  ◀ ▶ other orders`;
      hint.hidden = false;
      clearTimeout(hintTimer);
      hintTimer = setTimeout(hideHint, 6000);
    }

    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('keyup', onKeyUp);

    return { apply };
  })();

  function hideHint() {
    const hint = document.getElementById('remote-hint');
    if (hint) hint.hidden = true;
  }

  // settings page: every key press goes to .NET so it can show it
  // and assign it while "Change" is active

  let learning = false;
  let keyListener = null;

  function listenForKeys(dotnetRef) {
    stopListening();
    keyListener = (event) => {
      if (event.target.closest && event.target.closest('input, select, textarea')) return;
      const key = keyOf(event);
      if (learning) {
        event.preventDefault();
        event.stopImmediatePropagation();
      }
      dotnetRef.invokeMethodAsync('OnRemoteKey', key, keyName(key));
    };
    window.addEventListener('keydown', keyListener, true);
  }

  function stopListening() {
    if (keyListener) window.removeEventListener('keydown', keyListener, true);
    keyListener = null;
    learning = false;
  }

  function setLearning(on) {
    learning = !!on;
  }

  // apply saved settings before Blazor starts so there's no flash
  applySettings(loadSettings());

  window.KDS = {
    settings: { load: loadSettings, save: saveSettings, reset: resetSettings, defaultRemoteKeys },
    board: { attach: attachBoard, detach: detachBoard },
    remote: { listen: listenForKeys, stop: stopListening, setLearning, keyName },
    beep,
    timeZone,
    isNarrow,
  };
})();
