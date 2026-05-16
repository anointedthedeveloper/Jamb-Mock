const state = {
  studentExamId: null,
  sessionId: null,
  examId: null,
  durationMinutes: 0,
  questions: [],
  answers: {},
  flagged: {},
  currentIndex: 0,
  timerInterval: null,
  heartbeatInterval: null,
  secondsLeft: 0,
  submitted: false
};

// ── Persistence ────────────────────────────────────────────────────────────
function saveGlobalState() {
  localStorage.setItem('cbt_global_state', JSON.stringify(state));
}

function loadGlobalState() {
  const saved = localStorage.getItem('cbt_global_state');
  if (saved) {
    const data = JSON.parse(saved);
    Object.assign(state, data);
  }
}

// Load state immediately on every page
loadGlobalState();

// ── Helpers ────────────────────────────────────────────────────────────────
const $ = id => document.getElementById(id);
const showPage = id => {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  $(id).classList.add('active');
};

const storageKey = () => `cbt-progress-${state.studentExamId || 'draft'}`;

window.addEventListener('DOMContentLoaded', () => {
  const code = new URLSearchParams(location.search).get('code');
  if (code) $('inp-code').value = code.toUpperCase();
  $('waiting-status').textContent = 'Ready to continue when admin starts the exam.';
  initCamera();
});

// ── Anti-cheat: Tab Switch Detection ──────────────────────────────────────
document.addEventListener('visibilitychange', () => {
  if (document.hidden && state.studentExamId && !state.submitted) {
    fetch('/api/student/tabswitch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ studentExamId: state.studentExamId })
    });
    showWarning('Tab switching detected and logged.');
  }
});

// ── Anti-cheat: Fullscreen ─────────────────────────────────────────────────
function requestFullscreen() {
  const el = document.documentElement;
  if (el.requestFullscreen) el.requestFullscreen();
  else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
}

document.addEventListener('fullscreenchange', () => {
  if (!document.fullscreenElement && state.studentExamId && !state.submitted) {
    showWarning('Please stay in fullscreen mode.');
    setTimeout(requestFullscreen, 1500);
  }
});

// ── Anti-cheat: Prevent right-click & keyboard shortcuts ──────────────────
document.addEventListener('contextmenu', e => { if (state.studentExamId) e.preventDefault(); });
document.addEventListener('keydown', e => {
  if (!state.studentExamId) return;
  if ((e.ctrlKey || e.metaKey) && ['c','v','a','p','s','u'].includes(e.key.toLowerCase())) e.preventDefault();
  if (e.key === 'F12' || e.key === 'F5') e.preventDefault();
});

// ── Prevent accidental page refresh ───────────────────────────────────────
window.addEventListener('beforeunload', e => {
  if (state.studentExamId && !state.submitted) {
    e.preventDefault();
    e.returnValue = '';
  }
});

// ── Join Exam ──────────────────────────────────────────────────────────────
async function joinExam() {
  const code = $('inp-code').value.trim().toUpperCase();
  const name = $('inp-name').value.trim();
  const id   = $('inp-id').value.trim();
  const errEl = $('join-error');

  if (!code || !name || !id) { showError(errEl, 'All fields are required.'); return; }

  $('btn-join').disabled = true;
  $('btn-join').textContent = 'Joining...';

  try {
    const res = await fetch('/api/student/join', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionCode: code, fullName: name, studentId: id })
    });

    if (!res.ok) {
      const msg = await res.text();
      showError(errEl, msg || 'Failed to join. Check session code.');
      return;
    }

    const data = await res.json();
    state.studentExamId = data.studentExamId;
    state.sessionId = data.sessionId;
    state.examId = data.examId;
    state.durationMinutes = data.durationMinutes;

    $('exam-title').textContent = data.examTitle;
    errEl.classList.add('hidden');
    
    saveGlobalState();

    // Navigate to separate pages
    if (data.availableExams && data.availableExams.length > 1) {
      location.href = 'selection.html';
    } else {
      location.href = 'waiting.html';
    }

  } catch (err) {
    showError(errEl, 'Connection error. Is the server running?');
  } finally {
    $('btn-join').disabled = false;
    $('btn-join').textContent = 'Join Exam';
  }
}

async function selectExamSession(examId) {
  state.examId = examId;
  saveGlobalState();
  location.href = 'waiting.html';
}

function continueFromWaiting() {
  location.href = 'exam.html';
}

function startExamNow() {
  // If on index/waiting, go to exam.html
  if (!location.pathname.includes('exam.html')) {
    location.href = 'exam.html';
    return;
  }
  requestFullscreen();
  if (!state.timerInterval) startTimer(state.durationMinutes * 60);
  startHeartbeat();
  renderNavigator();
  renderQuestion();
}

function maybeResumePage() {
  const saved = loadLocalProgress();
  if (saved && Object.keys(saved.answers || {}).length > 0) {
    $('resume-status').textContent = `Saved answers found: ${Object.keys(saved.answers).length}`;
    showPage('page-resume');
    return;
  }
  startExamNow();
}

function resumeExam() {
  const saved = loadLocalProgress();
  if (saved) {
    state.answers = saved.answers || {};
    state.flagged = saved.flagged || {};
    state.currentIndex = saved.currentIndex || 0;
  }
  startExamNow();
}

function startExamNow() {
  requestFullscreen();
  showPage('page-exam');
  if (!state.timerInterval) startTimer(state.durationMinutes * 60);
  startHeartbeat();
  renderNavigator();
  renderQuestion();
}

// ── Load Questions ─────────────────────────────────────────────────────────
async function loadQuestions() {
  const res = await fetch(`/api/student/${state.studentExamId}/questions`);
  state.questions = await res.json();
  state.currentIndex = 0;
  renderNavigator();
  renderQuestion();
}

// ── Render Question Navigator ──────────────────────────────────────────────
function renderNavigator() {
  const nav = $('question-nav');
  nav.innerHTML = '<h4>Questions</h4><div class="nav-grid" id="nav-grid"></div>';
  const grid = $('nav-grid');
  state.questions.forEach((q, i) => {
    const btn = document.createElement('button');
    btn.className = 'nav-btn';
    btn.textContent = i + 1;
    btn.onclick = () => goToQuestion(i);
    btn.id = `nav-${i}`;
    grid.appendChild(btn);
  });
  updateNavigator();
}

function updateNavigator() {
  state.questions.forEach((q, i) => {
    const btn = $(`nav-${i}`);
    if (!btn) return;
    btn.className = 'nav-btn';
    if (state.answers[q.questionId] !== undefined) btn.classList.add('answered');
    if (state.flagged[q.questionId]) btn.classList.add('warning');
    if (i === state.currentIndex) btn.classList.add('current');
  });
  const answered = Object.keys(state.answers).length;
  $('progress-text').textContent = `${answered} / ${state.questions.length}`;
}

// ── Render Current Question ────────────────────────────────────────────────
function renderQuestion() {
  const q = state.questions[state.currentIndex];
  if (!q) return;

  const letters = ['A', 'B', 'C', 'D', 'E', 'F'];
  const selected = state.answers[q.questionId];

  $('question-container').innerHTML = `
    <div class="question-card">
      <div class="question-number">Question ${state.currentIndex + 1} of ${state.questions.length}</div>
      <div class="question-text">${escapeHtml(q.text)}</div>
      <div style="margin:8px 0;color:#64748B;">${state.flagged[q.questionId] ? 'Flagged for review' : ''}</div>
      <div class="options-list">
        ${q.options.map((opt, i) => `
          <div class="option-item ${selected === opt ? 'selected' : ''}" onclick="selectAnswer('${escapeAttr(opt)}')">
            <div class="option-letter">${letters[i] || i + 1}</div>
            <div class="option-text">${escapeHtml(opt)}</div>
          </div>
        `).join('')}
      </div>
    </div>`;

  $('btn-prev').disabled = state.currentIndex === 0;
  $('btn-next').disabled = state.currentIndex === state.questions.length - 1;
  updateNavigator();
}

// ── Answer Selection ───────────────────────────────────────────────────────
function selectAnswer(optionText) {
  const q = state.questions[state.currentIndex];
  state.answers[q.questionId] = optionText;
  saveProgressForQuestion(q.questionId, optionText);
  persistLocalProgress();
  renderQuestion();
}

function clearCurrentAnswer() {
  const q = state.questions[state.currentIndex];
  delete state.answers[q.questionId];
  persistLocalProgress();
  renderQuestion();
}

function toggleFlagCurrent() {
  const q = state.questions[state.currentIndex];
  state.flagged[q.questionId] = !state.flagged[q.questionId];
  persistLocalProgress();
  renderQuestion();
}

// ── Navigation ─────────────────────────────────────────────────────────────
function goToQuestion(i) { state.currentIndex = i; renderQuestion(); }
function prevQuestion() { if (state.currentIndex > 0) { state.currentIndex--; persistLocalProgress(); renderQuestion(); } }
function nextQuestion() { if (state.currentIndex < state.questions.length - 1) { state.currentIndex++; persistLocalProgress(); renderQuestion(); } }

// ── Timer ──────────────────────────────────────────────────────────────────
function startTimer(seconds) {
  state.secondsLeft = seconds;
  updateTimerDisplay();
  state.timerInterval = setInterval(() => {
    state.secondsLeft--;
    updateTimerDisplay();
    if (state.secondsLeft % 15 === 0) persistLocalProgress();
    if (state.secondsLeft <= 300) $('timer').parentElement.classList.add('warning');
    if (state.secondsLeft <= 0) { clearInterval(state.timerInterval); submitExam(); }
  }, 1000);
}

function updateTimerDisplay() {
  const m = Math.floor(state.secondsLeft / 60).toString().padStart(2, '0');
  const s = (state.secondsLeft % 60).toString().padStart(2, '0');
  $('timer').textContent = `${m}:${s}`;
}

// ── Submit ─────────────────────────────────────────────────────────────────
function confirmSubmit() {
  const unanswered = state.questions.length - Object.keys(state.answers).length;
  $('modal-msg').textContent = unanswered > 0
    ? `You have ${unanswered} unanswered question(s). Submit anyway?`
    : 'Are you sure you want to submit? You cannot change answers after submission.';
  $('modal-overlay').classList.remove('hidden');
}

function closeModal() { $('modal-overlay').classList.add('hidden'); }

async function submitExam() {
  closeModal();
  if (state.submitted) return;
  state.submitted = true;
  clearInterval(state.timerInterval);
  clearInterval(state.heartbeatInterval);

  const answers = Object.entries(state.answers).map(([qId, ans]) => ({
    questionId: parseInt(qId),
    selectedAnswer: ans
  }));

  try {
    const res = await fetch('/api/student/submit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ studentExamId: state.studentExamId, answers })
    });
    const result = await res.json();
    localStorage.removeItem(storageKey());
    showResult(result);
  } catch {
    showResult({ score: 0, total: state.questions.length, percentage: 0 });
  }
}

function showResult(result) {
  if (document.fullscreenElement) document.exitFullscreen?.();
  $('result-score').textContent = result.score;
  $('result-total').textContent = result.total;
  $('result-pct').textContent = `${result.percentage}%`;
  const pct = result.percentage;
  $('result-icon').textContent = pct >= 70 ? '🎉' : pct >= 50 ? '👍' : '📚';
  $('result-msg').textContent = pct >= 70 ? 'Excellent work!' : pct >= 50 ? 'Good effort!' : 'Keep studying!';
  renderReview();
  showPage('page-result');
}

function renderReview() {
  const host = $('review-list');
  host.innerHTML = state.questions.map((q, idx) => {
    const answer = state.answers[q.questionId] || 'No answer';
    return `<div style="margin:8px 0;padding:8px;border:1px solid #E5E7EB;border-radius:8px;">
      <strong>Q${idx + 1}:</strong> ${escapeHtml(q.text)}<br/>
      <span><strong>Your answer:</strong> ${escapeHtml(answer)}</span>
    </div>`;
  }).join('');
}

function saveAndExit() {
  persistLocalProgress();
  showWarning('Progress saved locally.');
  showPage('page-join');
}

async function saveProgressForQuestion(questionId, selectedAnswer) {
  if (!state.studentExamId) return;
  try {
    await fetch('/api/student/progress', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ studentExamId: state.studentExamId, questionId, selectedAnswer })
    });
  } catch { /* fallback to local storage */ }
}

async function loadServerProgress() {
  if (!state.studentExamId) return;
  try {
    const res = await fetch(`/api/student/${state.studentExamId}/progress`);
    if (!res.ok) return;
    const data = await res.json();
    (data.answers || []).forEach(a => { state.answers[a.questionId] = a.selectedAnswer; });
    persistLocalProgress();
  } catch { /* ignore */ }
}

function persistLocalProgress() {
  if (!state.studentExamId) return;
  localStorage.setItem(storageKey(), JSON.stringify({
    answers: state.answers,
    flagged: state.flagged,
    currentIndex: state.currentIndex
  }));
}

function loadLocalProgress() {
  try {
    const raw = localStorage.getItem(storageKey());
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function toggleCalculator() {
  $('calculator-modal').classList.remove('hidden');
}

function closeCalculator() {
  $('calculator-modal').classList.add('hidden');
}

function runCalculator() {
  const expr = $('calc-input').value.trim();
  if (!expr) return;
  try {
    const val = Function(`"use strict"; return (${expr})`)();
    $('calc-output').textContent = `Result: ${val}`;
  } catch {
    $('calc-output').textContent = 'Invalid expression.';
  }
}

async function initCamera() {
  const el = $('camera-preview');
  if (!el || !navigator.mediaDevices?.getUserMedia) return;
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
    el.srcObject = stream;
  } catch {
    // No camera available; preview remains empty.
  }
}

async function captureSnapshot() {
  const video = $('camera-preview');
  const canvas = $('camera-canvas');
  if (!video || !canvas) return;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;
  ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
  const dataUrl = canvas.toDataURL('image/jpeg', 0.7);
  try {
    await fetch('/api/student/snapshot', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ studentExamId: state.studentExamId, imageBase64: dataUrl })
    });
    showWarning('Snapshot captured.');
  } catch {
    showWarning('Snapshot saved locally only.');
  }
}

function startHeartbeat() {
  clearInterval(state.heartbeatInterval);
  state.heartbeatInterval = setInterval(async () => {
    if (!state.studentExamId || state.submitted) return;
    const battery = await getBatteryLevel();
    await fetch('/api/student/heartbeat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        studentExamId: state.studentExamId,
        currentQuestion: state.currentIndex + 1,
        batteryLevel: battery,
        isOnline: navigator.onLine,
        connectionState: navigator.onLine ? 'online' : 'offline',
        deviceName: getDeviceName(),
        deviceId: getOrCreateDeviceId()
      })
    });
  }, 10000);
}

function getOrCreateDeviceId() {
  let id = localStorage.getItem('cbt_device_id');
  if (!id) {
    id = 'DEV-' + Math.random().toString(36).substr(2, 9).toUpperCase();
    localStorage.setItem('cbt_device_id', id);
  }
  return id;
}

async function getBatteryLevel() {
  try {
    if (!navigator.getBattery) return -1;
    const b = await navigator.getBattery();
    return Math.round((b.level || 0) * 100);
  } catch {
    return -1;
  }
}

function getDeviceName() {
  const ua = navigator.userAgent;
  if (ua.includes('Windows')) return 'Windows PC';
  if (ua.includes('Macintosh')) return 'MacBook';
  if (ua.includes('Android')) return 'Android Device';
  if (ua.includes('iPhone')) return 'iPhone';
  if (ua.includes('iPad')) return 'iPad';
  return 'Web Client';
}

// ── Utilities ──────────────────────────────────────────────────────────────
function showError(el, msg) { el.textContent = msg; el.classList.remove('hidden'); }

let warningTimeout;
function showWarning(msg) {
  clearTimeout(warningTimeout);
  let w = $('warning-toast');
  if (!w) {
    w = document.createElement('div');
    w.id = 'warning-toast';
    w.style.cssText = 'position:fixed;top:70px;left:50%;transform:translateX(-50%);background:#EF4444;color:white;padding:10px 20px;border-radius:8px;font-weight:600;z-index:9999;font-size:14px;';
    document.body.appendChild(w);
  }
  w.textContent = msg;
  w.style.display = 'block';
  warningTimeout = setTimeout(() => { w.style.display = 'none'; }, 3000);
}

function escapeHtml(str) {
  return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function escapeAttr(str) {
  return String(str).replace(/'/g, '&#39;').replace(/"/g, '&quot;');
}
