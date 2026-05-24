'use strict';

const SAVE_KEY = 'cs-character';
const DICE_CYCLE = ['d4', 'd6', 'd8', 'd10', 'd12'];
const ROW_COUNT = 10;

// ── Default state ─────────────────────────────────────────────────
function defaultState() {
  return {
    name: '', lineage: '', hometown: '', flaws: '', summa: '',
    portrait: '',
    rows: Array.from({ length: ROW_COUNT }, () => ({
      equipName: '', equipSub: '',
      equipUsed: false,
      skillAdv: false,
      skillName: '', skillSub: '',
      die: 'd6',
    })),
    spells: ['', '', ''],
  };
}

let state = defaultState();

// ── Persistence ───────────────────────────────────────────────────
function save() {
  localStorage.setItem(SAVE_KEY, JSON.stringify(state));
}

function load() {
  const raw = localStorage.getItem(SAVE_KEY);
  if (!raw) return;
  try {
    const saved = JSON.parse(raw);
    state = Object.assign(defaultState(), saved);
    // Ensure rows array is correct length
    if (!Array.isArray(state.rows) || state.rows.length !== ROW_COUNT) {
      state.rows = defaultState().rows;
    }
    if (!Array.isArray(state.spells)) state.spells = ['', '', ''];
  } catch {
    state = defaultState();
  }
}

// ── Build rows ────────────────────────────────────────────────────
function buildRows() {
  const container = document.getElementById('body-rows');
  container.innerHTML = '';

  for (let i = 0; i < ROW_COUNT; i++) {
    const row = state.rows[i];

    const div = document.createElement('div');
    div.className = 'row';
    div.innerHTML = `
      <!-- Equipment side -->
      <div class="equip-side">
        <input class="row-name" type="text"
               placeholder="item…"
               data-row="${i}" data-field="equipName"
               value="${esc(row.equipName)}"
               autocomplete="off">
        <input class="row-sub" type="text"
               placeholder="notes…"
               data-row="${i}" data-field="equipSub"
               value="${esc(row.equipSub)}"
               autocomplete="off">
      </div>

      <!-- Center: advancement circles + spine -->
      <div class="center-col">
        <div class="adv-circle ${row.equipUsed ? 'filled' : ''}"
             data-row="${i}" data-field="equipUsed"
             title="Mark used"></div>
        <div class="adv-circle ${row.skillAdv ? 'filled' : ''}"
             data-row="${i}" data-field="skillAdv"
             title="Mark advancement"></div>
      </div>

      <!-- Skills side -->
      <div class="skill-side">
        <input class="row-name" type="text"
               placeholder="skill…"
               data-row="${i}" data-field="skillName"
               value="${esc(row.skillName)}"
               autocomplete="off">
        <input class="row-sub" type="text"
               placeholder="notes…"
               data-row="${i}" data-field="skillSub"
               value="${esc(row.skillSub)}"
               autocomplete="off">
      </div>

      <!-- Die rating -->
      <div class="die-col">
        <div class="die-bubble"
             data-row="${i}" data-field="die"
             data-die="${row.die}"
             title="Click to advance die">${row.die}</div>
      </div>
    `;
    container.appendChild(div);
  }
}

// ── Populate header inputs ────────────────────────────────────────
function populateHeader() {
  const fields = ['name', 'lineage', 'hometown', 'flaws', 'summa'];
  for (const f of fields) {
    const el = document.querySelector(`[data-save="${f}"]`);
    if (el) el.value = state[f] || '';
  }
  const spells = document.querySelectorAll('.spell-line');
  spells.forEach((el, i) => { el.value = state.spells[i] || ''; });
}

// ── Portrait ──────────────────────────────────────────────────────
function setupPortrait() {
  const box    = document.getElementById('portrait-box');
  const upload = document.getElementById('portrait-upload');
  const img    = document.getElementById('portrait-img');
  const hint   = document.querySelector('.portrait-hint');

  function showPortrait(dataUrl) {
    img.src = dataUrl;
    img.classList.add('visible');
    hint.style.display = 'none';
    state.portrait = dataUrl;
    save();
  }

  if (state.portrait) {
    showPortrait(state.portrait);
  }

  box.addEventListener('click', () => upload.click());

  upload.addEventListener('change', e => {
    const file = e.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = ev => showPortrait(ev.target.result);
    reader.readAsDataURL(file);
  });
}

// ── Event delegation ──────────────────────────────────────────────
function setupEvents() {
  // Header text inputs
  document.querySelectorAll('[data-save]').forEach(el => {
    el.addEventListener('input', () => {
      const key = el.dataset.save;
      if (key.startsWith('spell-')) {
        const idx = parseInt(key.split('-')[1], 10);
        state.spells[idx] = el.value;
      } else {
        state[key] = el.value;
      }
      save();
    });
  });

  // Row inputs and interactive elements (delegated)
  document.getElementById('body-rows').addEventListener('input', e => {
    const el = e.target;
    const i = parseInt(el.dataset.row, 10);
    if (isNaN(i)) return;
    state.rows[i][el.dataset.field] = el.value;
    save();
  });

  document.getElementById('body-rows').addEventListener('click', e => {
    const el = e.target;
    const i = parseInt(el.dataset.row, 10);
    if (isNaN(i)) return;

    // Advancement circle toggle
    if (el.classList.contains('adv-circle')) {
      const field = el.dataset.field;
      state.rows[i][field] = !state.rows[i][field];
      el.classList.toggle('filled', state.rows[i][field]);
      save();
      return;
    }

    // Die bubble cycle
    if (el.classList.contains('die-bubble')) {
      const current = state.rows[i].die;
      const idx = DICE_CYCLE.indexOf(current);
      const next = DICE_CYCLE[(idx + 1) % DICE_CYCLE.length];
      state.rows[i].die = next;
      el.dataset.die = next;
      el.textContent = next;
      save();
    }
  });
}

// ── Controls ──────────────────────────────────────────────────────
function setupControls() {
  document.getElementById('btn-clear').addEventListener('click', () => {
    if (!confirm('Clear this character sheet and start fresh?')) return;
    state = defaultState();
    save();
    init();
  });

  document.getElementById('btn-print').addEventListener('click', () => {
    window.print();
  });

  document.getElementById('btn-export').addEventListener('click', () => {
    const name = state.name || 'character';
    const blob = new Blob([JSON.stringify(state, null, 2)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `${name.replace(/\s+/g, '_')}_cs_sheet.json`;
    a.click();
    URL.revokeObjectURL(a.href);
  });

  document.getElementById('import-file').addEventListener('change', e => {
    const file = e.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = ev => {
      try {
        const imported = JSON.parse(ev.target.result);
        state = Object.assign(defaultState(), imported);
        save();
        init();
      } catch {
        alert('Could not read that file. Make sure it\'s a valid character JSON.');
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  });
}

// ── Helpers ───────────────────────────────────────────────────────
function esc(str) {
  return String(str || '')
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

// ── Init ──────────────────────────────────────────────────────────
function init() {
  buildRows();
  populateHeader();
  setupPortrait();
}

load();
init();
setupEvents();
setupControls();
