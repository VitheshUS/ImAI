const root = '/dev/database';
const sql = document.querySelector('#sql');
const status = document.querySelector('#status');
const results = document.querySelector('#results');
const run = document.querySelector('#run');
const tables = document.querySelector('#tables');
let activeTable;

function showStatus(message, error = false) {
  status.textContent = message;
  status.classList.toggle('error', error);
}

async function request(path, options) {
  const response = await fetch(root + path, { cache: 'no-store', ...options });
  const body = await response.json();
  if (!response.ok) throw new Error(body.detail || body.title || 'Request failed.');
  return body;
}

async function query() {
  if (run.disabled) return;
  run.disabled = true;
  showStatus('Running query…');
  results.replaceChildren();
  try {
    const data = await request('/query', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sql: sql.value }),
    });
    const table = document.createElement('table');
    const head = table.createTHead().insertRow();
    for (const name of data.columns) {
      const cell = document.createElement('th');
      cell.scope = 'col';
      cell.textContent = name;
      head.append(cell);
    }
    const body = table.createTBody();
    for (const row of data.rows) {
      const tr = body.insertRow();
      for (const value of row) {
        const cell = tr.insertCell();
        cell.textContent = value === null ? 'NULL' : String(value);
        cell.classList.toggle('null', value === null);
      }
    }
    results.append(table);
    showStatus(`${data.rows.length} row${data.rows.length === 1 ? '' : 's'}${data.truncated ? ' · Results limited to 500 rows. Add a WHERE clause to narrow your query.' : ''}`);
  } catch (error) {
    showStatus(error.message, true);
  } finally {
    run.disabled = false;
  }
}

function selectTable(table, button) {
  if (run.disabled) return;
  activeTable = table.name;
  for (const item of tables.children) item.removeAttribute('aria-current');
  button.setAttribute('aria-current', 'true');
  const schema = document.querySelector('#schema');
  schema.replaceChildren();
  const heading = document.createElement('h2');
  heading.textContent = table.name;
  const columns = document.createElement('div');
  columns.className = 'columns';
  for (const column of table.columns) {
    const badge = document.createElement('span');
    badge.className = 'column';
    badge.textContent = `${column.name} · ${column.type || 'ANY'}${column.primaryKey ? ' · PK' : ''}${column.notNull ? ' · NOT NULL' : ''}`;
    columns.append(badge);
  }
  schema.append(heading, columns);
  sql.value = `SELECT * FROM "${table.name.replaceAll('"', '""')}" LIMIT 100;`;
  void query();
}

async function refresh() {
  const button = document.querySelector('#refresh');
  button.disabled = true;
  try {
    const data = await request('/tables');
    tables.replaceChildren();
    for (const table of data) {
      const item = document.createElement('button');
      item.type = 'button';
      item.textContent = table.name;
      if (table.name === activeTable) item.setAttribute('aria-current', 'true');
      item.addEventListener('click', () => selectTable(table, item));
      tables.append(item);
    }
    if (!data.length) showStatus('This database has no tables yet.');
  } catch (error) {
    showStatus(error.message, true);
  } finally {
    button.disabled = false;
  }
}

document.querySelector('#query-form').addEventListener('submit', event => { event.preventDefault(); void query(); });
document.querySelector('#refresh').addEventListener('click', () => { void refresh(); });
sql.addEventListener('keydown', event => {
  if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); void query(); }
});
void refresh();
