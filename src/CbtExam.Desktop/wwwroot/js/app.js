// --- Dynamic API Base configuration ---
let API_BASE = '/api';

// Auto-detect and fall back to saved Server IP if opened via file:// protocol
if (window.location.protocol === 'file:') {
    const savedServerIp = localStorage.getItem('cbt_server_ip') || 'localhost';
    API_BASE = `http://${savedServerIp}:5000/api`;
}

// --- Device fingerprinting and heartbeat locking (wrapped in try-catch to prevent crashes in private modes) ---
let deviceId = 'NODE-UNKNOWN';
try {
    deviceId = localStorage.getItem('cbt_device_id');
    if (!deviceId) {
        deviceId = 'NODE-' + Math.random().toString(36).substring(2, 11).toUpperCase();
        localStorage.setItem('cbt_device_id', deviceId);
    }
} catch (e) {
    deviceId = 'NODE-' + Math.random().toString(36).substring(2, 11).toUpperCase();
}

function getBrowserAndOS() {
    const ua = navigator.userAgent;
    let browser = "Browser";

    if (ua.indexOf("Firefox") > -1) browser = "Firefox";
    else if (ua.indexOf("Chrome") > -1 && ua.indexOf("Edge") === -1 && ua.indexOf("Edg") === -1) browser = "Chrome";
    else if (ua.indexOf("Safari") > -1 && ua.indexOf("Chrome") === -1) browser = "Safari";
    else if (ua.indexOf("Edge") > -1 || ua.indexOf("Edg") > -1) browser = "Edge";
    else if (ua.indexOf("Trident") > -1 || ua.indexOf("MSIE") > -1) browser = "IE";

    return browser;
}

async function runDeviceHeartbeat() {
    const browserOS = getBrowserAndOS();
    const studentId = localStorage.getItem('studentId') || "Awaiting Login";
    
    let batteryLevel = 100;
    try {
        if (navigator.getBattery) {
            const battery = await navigator.getBattery();
            batteryLevel = Math.round(battery.level * 100);
        }
    } catch (e) { }

    try {
        await fetch(`${API_BASE}/Student/device`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                deviceId: deviceId,
                deviceName: browserOS,
                batteryLevel: batteryLevel,
                studentId: studentId
            })
        });
    } catch (e) {
        console.warn("LAN device heartbeat failed", e);
    }
}

// --- Smart User & Session Detection ---
function runSmartSessionDetection() {
    const currentPath = window.location.pathname;
    const isLoginPage = currentPath.endsWith('index.html') || currentPath.endsWith('/') || currentPath === '';
    const hasActiveUser = localStorage.getItem('studentId');

    if (isLoginPage && hasActiveUser) {
        window.location.href = 'selection.html';
        return true;
    }

    if (!isLoginPage && !hasActiveUser) {
        if (window.location.protocol !== 'file:') {
            window.location.href = 'index.html';
            return true;
        }
    }
    return false;
}

// --- Initialization ---
document.addEventListener('DOMContentLoaded', () => {
    if (runSmartSessionDetection()) return;
    
    updateDynamicYear();
    initConnectionSettings();
    
    // Check if we are on the selection page
    if (document.getElementById('examList')) {
        initializeSelectionPage();
    }
    
    console.log('JAMB CBT Portal Initialized');
    
    // Start device heartbeat loop
    runDeviceHeartbeat();
    setInterval(runDeviceHeartbeat, 5000);

    // --- SignalR: instant broadcast receiver ---
    try {
        const hubUrl = API_BASE.replace('/api', '') + '/hubs/exam';
        const examHub = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        examHub.on('BroadcastMessage', (payload) => {
            const msg = payload?.message || payload;
            if (!msg) return;
            const lastBroadcast = localStorage.getItem('last_broadcast_msg');
            if (lastBroadcast !== msg) {
                localStorage.setItem('last_broadcast_msg', msg);
                if (typeof showToast === 'function')
                    showToast('📢 Broadcast from Coordinator', msg, 'info');
            }
        });

        examHub.start().catch(() => { /* offline — heartbeat poll is fallback */ });
    } catch (e) { /* signalR not loaded (e.g. on non-exam pages) */ }
});

// --- Connection Settings for file:// Protocol ---
function initConnectionSettings() {
    const box = document.getElementById('connectionSettings');
    const input = document.getElementById('serverIpInput');
    if (!box || !input) return;

    if (window.location.protocol === 'file:') {
        box.style.display = 'block';
        const saved = localStorage.getItem('cbt_server_ip') || 'localhost';
        input.value = saved;
    }
}

function saveServerIp() {
    const input = document.getElementById('serverIpInput');
    if (!input) return;
    const ip = input.value.trim();
    if (ip) {
        localStorage.setItem('cbt_server_ip', ip);
        showToast('Connection Saved', `Connected to coordinator server at http://${ip}:5000`, 'success');
        setTimeout(() => {
            window.location.reload();
        }, 1000);
    }
}

// --- UI Utilities ---
function updateDynamicYear() {
    const yearElements = document.querySelectorAll('#currentYear');
    const year = new Date().getFullYear();
    yearElements.forEach(el => el.textContent = year);
}

function togglePasswordVisibility() {
    const passwordInput = document.getElementById('password');
    const eyeIcon = document.getElementById('eyeIcon');
    
    if (passwordInput.type === 'password') {
        passwordInput.type = 'text';
        eyeIcon.innerHTML = `<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/>`;
    } else {
        passwordInput.type = 'password';
        eyeIcon.innerHTML = `<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>`;
    }
}

/**
 * Modern Toast Notification System
 * @param {string} title 
 * @param {string} message 
 * @param {string} type - 'success', 'error', 'info'
 */
function showToast(title, message, type = 'info') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    
    const icon = type === 'success' 
        ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>'
        : type === 'error'
        ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>'
        : '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';

    toast.innerHTML = `
        <div class="toast-icon">${icon}</div>
        <div class="toast-content">
            <div class="toast-title">${title}</div>
            <div class="toast-message">${message}</div>
        </div>
    `;

    container.appendChild(toast);

    // Auto remove after 5 seconds
    setTimeout(() => {
        toast.style.animation = 'toastFadeOut 0.5s ease forwards';
        setTimeout(() => toast.remove(), 500);
    }, 5000);
}

// --- Authentication ---
async function handleLogin(event) {
    event.preventDefault();
    
    const studentId = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value.trim();
    const btn = document.getElementById('submitBtn');
    
    if (!studentId || !password) {
        showToast('Required', 'Please enter your student ID and password.', 'error');
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<div class="spinner"></div><span>Authenticating...</span>';

    try {
        // Use the dedicated student login endpoint (singular Student)
        const response = await fetch(`${API_BASE}/Student/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ studentId, password })
        });

        if (response.ok) {
            const user = await response.json();
            localStorage.setItem('studentName', user.fullName);
            localStorage.setItem('studentId', user.studentId);
            
            showToast('Success', `Welcome back, ${user.fullName.split(' ')[0]}!`, 'success');
            
            setTimeout(() => {
                window.location.href = 'selection.html';
            }, 1000);
        } else {
            const err = await response.json();
            showToast('Login Failed', err.error || 'Invalid credentials or account inactive.', 'error');
            resetLoginButton(btn);
        }
    } catch (error) {
        console.error('Login error:', error);
        showToast('Connection Error', 'Unable to reach the server. Please try again later.', 'error');
        resetLoginButton(btn);
    }
}

function resetLoginButton(btn) {
    btn.disabled = false;
    btn.innerHTML = '<span>Sign In</span><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/></svg>';
}

// --- Selection Page Logic ---
async function initializeSelectionPage() {
    const studentName = localStorage.getItem('studentName') || 'Candidate';
    document.getElementById('userName').textContent = studentName;
    document.getElementById('userAvatar').textContent = studentName.charAt(0).toUpperCase();
    
    await fetchAndRenderExams();
    
    // Poll for new exams/sessions every 4 seconds to instantly detect started exam sessions!
    setInterval(fetchAndRenderExams, 4000);
}

async function fetchAndRenderExams() {
    const listContainer = document.getElementById('examList');
    
    try {
        const response = await fetch(`${API_BASE}/Exams?activeOnly=true`);
        if (!response.ok) throw new Error('API Error');
        
        const exams = await response.json();
        
        if (exams.length === 0) {
            listContainer.innerHTML = '<div class="empty-state">No examinations are currently scheduled.</div>';
            return;
        }

        listContainer.innerHTML = '';
        exams.forEach(exam => {
            const card = createExamCard(exam);
            listContainer.appendChild(card);
        });
    } catch (error) {
        console.error('Fetch exams error:', error);
        listContainer.innerHTML = '<div class="error-state">Failed to load examinations. Please check your connection.</div>';
        showToast('Error', 'Failed to synchronize available examinations.', 'error');
    }
}

function createExamCard(exam) {
    const div = document.createElement('div');
    div.className = 'exam-card-modern';
    div.onclick = () => {
        localStorage.setItem('selectedExamId', exam.id);
        localStorage.setItem('selectedExamTitle', exam.title);
        window.location.href = 'waiting.html';
    };

    div.innerHTML = `
        <div class="exam-card-info">
            <div class="exam-type-badge">ACTIVE</div>
            <h3>${exam.title}</h3>
            <div class="exam-meta-pills">
                <span class="meta-pill">${exam.questionCount || 0} Questions</span>
                <span class="meta-pill">${exam.durationMinutes} Minutes</span>
            </div>
        </div>
                <button class="action-btn">Join Exam</button>
    `;
    
    return div;
}

// ── Candidate Examination Runner Engine ──

let questions = [];
let currentIndex = 0;
let studentExamId = null;
let studentId = null;
let studentName = null;
let examDuration = 60; // default 60 minutes
let timeRemaining = 3600;
let timerInterval = null;
let responses = {};
let flagged = {};
let examCompleted = false;
let heartbeatInterval = null;
let subjects = [];
let activeSubject = '';

// Add initialization checking for exam page
document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('question-nav')) {
        initializeExamPage();
    }
});

async function initializeExamPage() {
    studentExamId = localStorage.getItem('studentExamId');
    studentId = localStorage.getItem('studentId') || '00000000';
    studentName = localStorage.getItem('studentName') || 'Candidate';
    examDuration = parseInt(localStorage.getItem('examDuration')) || 60;
    
    // Set user info in header
    document.getElementById('user-name').textContent = studentName;
    document.getElementById('user-id').textContent = `ID: ${studentId}`;
    const avatar = document.getElementById('user-avatar-initial');
    if (avatar) avatar.textContent = studentName.charAt(0).toUpperCase();

    // Load cached questions
    const cached = localStorage.getItem('cachedQuestions');
    if (cached) {
        questions = JSON.parse(cached);
    } else {
        // Fallback fetch
        try {
            const res = await fetch(`${API_BASE}/Student/${studentExamId}/questions`);
            if (res.ok) {
                questions = await res.json();
                localStorage.setItem('cachedQuestions', JSON.stringify(questions));
            } else {
                showToast('Error', 'Could not load examination questions.', 'error');
                return;
            }
        } catch (err) {
            console.error(err);
            showToast('Error', 'Connection failure. Could not download questions.', 'error');
            return;
        }
    }

    if (questions.length === 0) {
        showToast('Empty Exam', 'There are no questions in this examination.', 'error');
        return;
    }

    // Load saved answers
    const savedAnswers = localStorage.getItem(`studentExamAnswers_${studentExamId}`);
    if (savedAnswers) {
        responses = JSON.parse(savedAnswers);
    } else {
        // Load initial progress from server in case of refresh or device switch
        try {
            const progressRes = await fetch(`${API_BASE}/Student/${studentExamId}/progress`);
            if (progressRes.ok) {
                const prog = await progressRes.json();
                if (prog.answers) {
                    prog.answers.forEach(a => {
                        responses[a.questionId] = a.selectedAnswer;
                    });
                    localStorage.setItem(`studentExamAnswers_${studentExamId}`, JSON.stringify(responses));
                }
            }
        } catch (e) {
            console.warn("Could not load initial progress from API", e);
        }
    }

    // Initialize UI
    currentIndex = 0;
    initSubjectTabs();
    renderNavigator();
    renderQuestion(currentIndex);
    updateProgressRing();
    
    // Start countdown and backend updates
    startTimer();
    startHeartbeat();
    setupEventListeners();
}

function setupEventListeners() {
    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
        if (examCompleted) return;
        
        // If calculator is open, do not trigger exam shortcuts
        const calcModal = document.getElementById('calculator-modal');
        if (calcModal && !calcModal.classList.contains('hidden')) {
            return;
        }

        const key = e.key.toUpperCase();
        if (key === 'A' || key === 'B' || key === 'C' || key === 'D') {
            const currentQ = questions[currentIndex];
            if (currentQ && currentQ.options) {
                const optIndex = key.charCodeAt(0) - 65; // 0, 1, 2, 3
                if (optIndex < currentQ.options.length) {
                    selectOption(currentQ.questionId, currentQ.options[optIndex]);
                }
            }
        } else if (key === 'N' || e.key === 'ArrowRight') {
            nextQuestion();
        } else if (key === 'P' || e.key === 'ArrowLeft') {
            prevQuestion();
        } else if (key === 'S') {
            confirmSubmit();
        }
    });

    // Window blur tab-switch reporting
    document.addEventListener('visibilitychange', () => {
        if (document.hidden && !examCompleted) {
            fetch(`${API_BASE}/Student/tabswitch`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ studentExamId: parseInt(studentExamId) })
            }).catch(err => console.error('Tab switch report error', err));
            
            showToast('Warning', 'Tab switching detected! This incident has been logged by the coordinator.', 'error');
        }
    });
}

function startTimer() {
    let examStart = localStorage.getItem(`examStart_${studentExamId}`);
    if (!examStart) {
        examStart = Date.now();
        localStorage.setItem(`examStart_${studentExamId}`, examStart);
    }
    
    const durationSeconds = examDuration * 60;
    
    const updateTimerDisplay = () => {
        const elapsedSeconds = Math.floor((Date.now() - parseInt(examStart)) / 1000);
        timeRemaining = durationSeconds - elapsedSeconds;
        
        if (timeRemaining <= 0) {
            timeRemaining = 0;
            document.getElementById('timer').textContent = "00:00:00";
            clearInterval(timerInterval);
            // Automatic submit on timeout
            submitExam(false);
            return;
        }
        
        const hrs = Math.floor(timeRemaining / 3600);
        const mins = Math.floor((timeRemaining % 3600) / 60);
        const secs = timeRemaining % 60;
        
        document.getElementById('timer').textContent = 
            `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
            
        // Alert visually in final 5 minutes
        if (timeRemaining < 300) {
            document.getElementById('timer').parentElement.classList.add('warning');
        }
    };
    
    updateTimerDisplay();
    timerInterval = setInterval(updateTimerDisplay, 1000);
}

function startHeartbeat() {
    const sendHeartbeat = async () => {
        if (examCompleted) return;
        try {
            const activeDeviceId = localStorage.getItem('cbt_device_id') || 'NODE-UNKNOWN';
            const activeDeviceName = getBrowserAndOS();
            let batteryLevel = 100;
            try {
                if (navigator.getBattery) {
                    const battery = await navigator.getBattery();
                    batteryLevel = Math.round(battery.level * 100);
                }
            } catch (e) { }

            const response = await fetch(`${API_BASE}/Student/heartbeat`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    studentExamId: parseInt(studentExamId),
                    currentQuestion: currentIndex + 1,
                    batteryLevel: batteryLevel,
                    isOnline: true,
                    connectionState: "Excellent",
                    deviceName: activeDeviceName,
                    deviceId: activeDeviceId
                })
            });
            if (response.ok) {
                const data = await response.json();
                if (data && data.broadcastMessage) {
                    const lastBroadcast = localStorage.getItem('last_broadcast_msg');
                    if (lastBroadcast !== data.broadcastMessage) {
                        localStorage.setItem('last_broadcast_msg', data.broadcastMessage);
                        showToast('Broadcast from Coordinator', data.broadcastMessage, 'info');
                    }
                }
            }
        } catch (e) {
            console.warn("Heartbeat update failed.", e);
        }
    };
    
    sendHeartbeat();
    heartbeatInterval = setInterval(sendHeartbeat, 10000);
}

function initSubjectTabs() {
    // Find all unique subjects in the questions
    subjects = [...new Set(questions.map(q => q.subject || 'General'))];
    
    // Sort subjects so English is first, then Mathematics, then others
    subjects.sort((a, b) => {
        if (a.toLowerCase() === 'english') return -1;
        if (b.toLowerCase() === 'english') return 1;
        return a.localeCompare(b);
    });

    activeSubject = subjects[0];
    renderSubjectTabs();
}

function renderSubjectTabs() {
    const container = document.getElementById('subject-tabs-bar');
    if (!container) return;
    container.innerHTML = '';

    subjects.forEach(sub => {
        const btn = document.createElement('button');
        btn.className = `btn btn-tab ${sub === activeSubject ? 'active' : ''}`;
        btn.textContent = sub;
        btn.onclick = () => switchSubject(sub);
        
        // Custom styling for elegant glassmorphic/flat modern tab buttons
        btn.style.padding = '10px 20px';
        btn.style.borderRadius = '10px';
        btn.style.fontWeight = '700';
        btn.style.cursor = 'pointer';
        btn.style.transition = 'all 0.2s ease';
        btn.style.fontSize = '13px';
        btn.style.textTransform = 'uppercase';
        btn.style.letterSpacing = '0.5px';
        
        if (sub === activeSubject) {
            btn.style.background = '#10B981'; // Sleek premium emerald green
            btn.style.color = '#ffffff';
            btn.style.border = 'none';
            btn.style.boxShadow = '0 4px 12px rgba(16, 185, 129, 0.25)';
        } else {
            btn.style.background = 'rgba(255, 255, 255, 0.08)';
            btn.style.color = '#94A3B8';
            btn.style.border = '1px solid rgba(255, 255, 255, 0.1)';
        }

        container.appendChild(btn);
    });
}

function switchSubject(sub) {
    activeSubject = sub;
    renderSubjectTabs();
    
    // Jump to first question of this subject
    const subQuestions = getFilteredQuestions();
    const firstSubQ = subQuestions[0];
    if (firstSubQ) {
        const globalIndex = questions.findIndex(q => q.questionId === firstSubQ.questionId);
        if (globalIndex !== -1) {
            currentIndex = globalIndex;
            renderQuestion(currentIndex);
            renderNavigator();
        }
    }
}

function getFilteredQuestions() {
    return questions.filter(q => (q.subject || 'General') === activeSubject);
}

function renderNavigator() {
    const grid = document.getElementById('question-nav');
    if (!grid) return;
    grid.innerHTML = "";

    const subQuestions = getFilteredQuestions();
    subQuestions.forEach((q, subIndex) => {
        const btn = document.createElement('button');
        btn.className = 'nav-btn';
        
        const qId = q.questionId;
        const globalIndex = questions.findIndex(x => x.questionId === qId);
        const isCurrent = globalIndex === currentIndex;
        const isAnswered = responses[qId] !== undefined;
        const isFlagged = flagged[qId] === true;

        if (isCurrent) btn.classList.add('current');
        if (isAnswered) btn.classList.add('answered');
        if (isFlagged) btn.classList.add('flagged');

        btn.textContent = subIndex + 1;
        btn.onclick = () => jumpToQuestion(globalIndex);
        grid.appendChild(btn);
    });

    // Update counts
    const totalCount = questions.length;
    const answeredCount = Object.keys(responses).length;
    document.getElementById('total-count').textContent = totalCount;
    document.getElementById('answered-count').textContent = answeredCount;
}

function renderQuestion(index) {
    const q = questions[index];
    if (!q) return;

    // Auto-select subject tab if we navigated to a question belonging to another subject
    const sub = q.subject || 'General';
    if (sub !== activeSubject) {
        activeSubject = sub;
        renderSubjectTabs();
    }

    const subQuestions = getFilteredQuestions();
    const subIndex = subQuestions.findIndex(x => x.questionId === q.questionId);

    document.getElementById('current-q-num').textContent = `${subIndex + 1} (${sub})`;
    document.getElementById('question-text').textContent = q.text;

    // Toggle Flag UI status
    const flagBtn = document.getElementById('btn-flag');
    if (flagBtn) {
        if (flagged[q.questionId]) {
            flagBtn.classList.add('flagged');
            flagBtn.style.color = '#EF4444';
        } else {
            flagBtn.classList.remove('flagged');
            flagBtn.style.color = '';
        }
    }

    renderOptions(q);
}

function renderOptions(question) {
    const list = document.getElementById('options-list');
    if (!list) return;
    list.innerHTML = "";
    
    const selectedAnswer = responses[question.questionId];
    
    question.options.forEach((optText, index) => {
        const letter = String.fromCharCode(65 + index); // A, B, C, D
        const isSelected = selectedAnswer === optText;
        
        const item = document.createElement('div');
        item.className = `option-item ${isSelected ? 'selected' : ''}`;
        item.onclick = () => selectOption(question.questionId, optText);
        
        item.innerHTML = `
            <div class="option-letter">${letter}</div>
            <div class="option-text">${optText}</div>
        `;
        list.appendChild(item);
    });
}

async function selectOption(questionId, optionText) {
    if (examCompleted) return;
    
    responses[questionId] = optionText;
    localStorage.setItem(`studentExamAnswers_${studentExamId}`, JSON.stringify(responses));
    
    // Refresh UIs
    const currentQ = questions[currentIndex];
    renderOptions(currentQ);
    renderNavigator();
    updateProgressRing();
    
    // Save progress to local DB
    try {
        await fetch(`${API_BASE}/Student/progress`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                studentExamId: parseInt(studentExamId),
                questionId: questionId,
                selectedAnswer: optionText
            })
        });
    } catch (e) {
        console.warn("Network offline. Option saved locally.", e);
    }
}

async function clearCurrentAnswer() {
    if (examCompleted) return;
    const currentQ = questions[currentIndex];
    if (!currentQ) return;
    
    delete responses[currentQ.questionId];
    localStorage.setItem(`studentExamAnswers_${studentExamId}`, JSON.stringify(responses));
    
    renderOptions(currentQ);
    renderNavigator();
    updateProgressRing();
    
    try {
        await fetch(`${API_BASE}/Student/progress`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                studentExamId: parseInt(studentExamId),
                questionId: currentQ.questionId,
                selectedAnswer: ""
            })
        });
    } catch (e) {
        console.warn("Failed to update clear selection in API", e);
    }
}

function toggleFlagCurrent() {
    if (examCompleted) return;
    const currentQ = questions[currentIndex];
    if (!currentQ) return;

    flagged[currentQ.questionId] = !flagged[currentQ.questionId];
    renderQuestion(currentIndex);
    renderNavigator();
}

function jumpToQuestion(index) {
    if (index < 0 || index >= questions.length) return;
    currentIndex = index;
    renderQuestion(currentIndex);
    renderNavigator();
}

function nextQuestion() {
    if (currentIndex < questions.length - 1) {
        jumpToQuestion(currentIndex + 1);
    }
}

function prevQuestion() {
    if (currentIndex > 0) {
        jumpToQuestion(currentIndex - 1);
    }
}

function updateProgressRing() {
    const ring = document.querySelector('.progress-ring__circle');
    const pctSpan = document.getElementById('progress-pct');
    if (!ring || !pctSpan) return;

    const total = questions.length;
    if (total === 0) return;

    const answered = Object.keys(responses).length;
    const pct = Math.round((answered / total) * 100);
    pctSpan.textContent = `${pct}%`;

    const radius = ring.r.baseVal.value;
    const circumference = radius * 2 * Math.PI;
    
    ring.style.strokeDasharray = `${circumference} ${circumference}`;
    const offset = circumference - (pct / 100) * circumference;
    ring.style.strokeDashoffset = offset;
}

// ── Scientific Calculator UI Implementation ──

let calcExpression = "";
function toggleCalculator() {
    const modal = document.getElementById('calculator-modal');
    if (modal) {
        modal.classList.toggle('hidden');
        if (!modal.classList.contains('hidden')) {
            calcExpression = "";
            document.getElementById('calc-display').textContent = "0";
        }
    }
}

function pressCalc(val) {
    const display = document.getElementById('calc-display');
    if (!display) return;
    
    if (val === 'C') {
        calcExpression = "";
        display.textContent = "0";
    } else if (val === '=') {
        try {
            let expr = calcExpression.replace(/√/g, 'Math.sqrt');
            expr = expr.replace(/sin\(/g, 'Math.sin(');
            expr = expr.replace(/cos\(/g, 'Math.cos(');
            expr = expr.replace(/tan\(/g, 'Math.tan(');
            expr = expr.replace(/log\(/g, 'Math.log10(');
            expr = expr.replace(/ln\(/g, 'Math.log(');
            
            let result = eval(expr);
            if (result === undefined || isNaN(result)) {
                display.textContent = "Error";
            } else {
                result = Math.round(result * 100000000) / 100000000;
                display.textContent = result.toString();
                calcExpression = result.toString();
            }
        } catch (e) {
            display.textContent = "Error";
        }
    } else if (['sin', 'cos', 'tan', 'log', 'ln', 'sqrt'].includes(val)) {
        if (val === 'sqrt') {
            calcExpression += "√(";
        } else {
            calcExpression += val + "(";
        }
        display.textContent = calcExpression;
    } else {
        if (calcExpression === "0" && !isNaN(val)) {
            calcExpression = val;
        } else {
            calcExpression += val;
        }
        display.textContent = calcExpression;
    }
}

// ── Submit Exam Logic ──

function confirmSubmit() {
    if (examCompleted) return;
    const modal = document.getElementById('submit-confirm-modal');
    if (modal) modal.classList.remove('hidden');
}

function closeSubmitConfirm() {
    const modal = document.getElementById('submit-confirm-modal');
    if (modal) modal.classList.add('hidden');
}

function closeModalOnOuterClick(event, modalId) {
    if (event.target.id === modalId) {
        if (modalId === 'calculator-modal') {
            toggleCalculator();
        } else if (modalId === 'submit-confirm-modal') {
            closeSubmitConfirm();
        }
    }
}

async function submitExam(manual = true) {
    if (examCompleted) return;
    examCompleted = true;
    
    // Disable inputs and overlays
    closeSubmitConfirm();
    clearInterval(timerInterval);
    clearInterval(heartbeatInterval);

    // Collect answers
    const answersList = [];
    questions.forEach(q => {
        const ans = responses[q.questionId];
        if (ans !== undefined) {
            answersList.push({
                questionId: q.questionId,
                selectedAnswer: ans
            });
        }
    });

    try {
        const submitRes = await fetch(`${API_BASE}/Student/submit`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                studentExamId: parseInt(studentExamId),
                answers: answersList
            })
        });

        if (submitRes.ok) {
            const scoreResult = await submitRes.json();
            
            // Save final scores and details in localStorage for high-fidelity client-side review!
            localStorage.setItem('lastExamResult', JSON.stringify({
                examTitle: localStorage.getItem('selectedExamTitle') || 'Examination',
                studentName: localStorage.getItem('studentName') || 'Candidate',
                studentId: localStorage.getItem('studentId') || '00000000',
                score: scoreResult.score,
                total: scoreResult.totalQuestions,
                percentage: scoreResult.percentage,
                submittedAt: new Date().toLocaleString(),
                answers: answersList,
                questions: questions
            }));

            // Display animated completion card
            document.getElementById('score-text').textContent = `${scoreResult.percentage}%`;
            document.getElementById('completion-modal').classList.remove('hidden');
        } else {
            showToast('Submission Failed', 'Server rejected direct submit. Retrying with local payload...', 'error');
            examCompleted = false; // allow retry
        }
    } catch (e) {
        console.error(e);
        showToast('Connection Failure', 'Could not sync submit. Please alert the exam invigilator.', 'error');
        examCompleted = false;
    }
}

function exitExamPortal() {
    // Clean up student local mock workspace
    localStorage.removeItem('cachedQuestions');
    localStorage.removeItem(`studentExamAnswers_${studentExamId}`);
    localStorage.removeItem(`examStart_${studentExamId}`);
    localStorage.removeItem('studentExamId');
    localStorage.removeItem('selectedExamId');
    localStorage.removeItem('selectedExamTitle');
    localStorage.removeItem('examDuration');
    
    window.location.href = 'results.html';
}
