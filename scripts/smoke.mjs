// Smoke test against a running LeaveFlow: SPA served, demo login, employee flow, manager approve.
const B = process.argv[2] ?? 'http://localhost:8080';
const j = (r) => r.json();
const post = (url, body, token) => fetch(B + url, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
  body: JSON.stringify(body ?? {}),
});
const get = (url, token) => fetch(B + url, { headers: token ? { Authorization: 'Bearer ' + token } : {} });

let ok = true;
const check = (name, cond, extra = '') => { console.log((cond ? 'PASS ' : 'FAIL ') + name, extra); ok &&= !!cond; };

const home = await fetch(B + '/');
check('SPA index', home.status === 200 && (await home.text()).includes('<app-root'));
check('deep link served', (await fetch(B + '/team')).status === 200);

const emp = await j(await post('/api/auth/demo', { role: 'Employee' }));
check('demo employee login', emp.user?.email === 'employee@leaveflow.demo');
const bal = await j(await get('/api/balances', emp.token));
check('balances', Array.isArray(bal) && bal.length === 3, bal.map((b) => `${b.nameTh}:${b.remaining}`).join(' '));

const sick = bal.find((b) => b.nameTh === 'ลาป่วย');
const today = new Date();
let d = new Date(today); d.setDate(d.getDate() + 14);
while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1);
const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const created = await post('/api/requests', { leaveTypeId: sick.leaveTypeId, start: iso, end: iso, half: 'Morning', reason: 'smoke' }, emp.token);
const req = await j(created);
check('create request', created.status === 201 && req.status === 'Pending', `${iso} ${req.days}d`);

const mgr = await j(await post('/api/auth/demo', { role: 'Manager' }));
const appr = await post(`/api/team/requests/${req.id}/approve`, { note: 'smoke ok' }, mgr.token);
check('manager approves', appr.status === 200 && (await j(appr)).status === 'Approved');

const hr = await j(await post('/api/auth/demo', { role: 'HrAdmin' }));
const csv = await get(`/api/hr/reports/monthly?month=${iso.slice(0, 7)}&format=csv`, hr.token);
check('csv report', csv.status === 200 && csv.headers.get('content-type')?.startsWith('text/csv'));

process.exit(ok ? 0 : 1);
