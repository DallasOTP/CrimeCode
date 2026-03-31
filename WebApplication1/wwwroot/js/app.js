// === CrimeMarket — Frontend Application ===

// === DASHBOARD SYSTEM ===
let dashWallets = {};
let dashCurrentSection = 'account';

document.addEventListener('DOMContentLoaded', () => {
    // Dashboard sidebar navigation (delegated)
    const menu = document.getElementById('dashboardMenu');
    if (menu) {
        menu.addEventListener('click', function (e) {
            const link = e.target.closest('.dash-link');
            if (!link || link.classList.contains('dash-logout')) return;
            e.preventDefault();
            menu.querySelectorAll('.dash-link').forEach(a => a.classList.remove('active'));
            link.classList.add('active');
            const section = link.dataset.section;
            if (section) showDashSection(section);
        });
    }
    // Withdraw currency change → show balance
    const wCur = document.getElementById('dashWithdrawCurrency');
    if (wCur) wCur.addEventListener('change', updateWithdrawBalance);
});

function showDashSection(section) {
    dashCurrentSection = section;
    document.querySelectorAll('.dash-section').forEach(s => s.style.display = 'none');
    const el = document.getElementById('dashSection-' + section);
    if (el) el.style.display = '';
    // Load data for section
    if (section === 'account') loadDashAccount();
    if (section === 'wallet-internal') loadDashWallet();
    if (section === 'deposit') { /* static form, no load needed */ }
    if (section === 'withdraw') { loadDashWalletQuiet(); updateWithdrawBalance(); }
    if (section === 'orders') loadDashOrders('buying');
    if (section === 'messenger') loadDashMessenger();
    if (section === 'tickets') loadDashTickets();
    if (section === 'block') loadDashBlockedUsers();
    if (section === 'notifications') loadDashNotifications();
}

function initDashboard() {
    showDashSection('account');
    loadDashSidebar();
}

function loadDashSidebar() {
    if (!currentUser) return;
    const nameEl = document.getElementById('dashUsernameDisplay');
    const roleEl = document.getElementById('dashRoleDisplay');
    const avEl = document.getElementById('dashAvatarDisplay');
    if (nameEl) nameEl.textContent = currentUser.username || 'User';
    if (roleEl) roleEl.textContent = currentUser.isVendor ? 'Vendor' : 'Member';
    if (avEl) {
        if (currentUser.avatar) {
            avEl.style.backgroundImage = 'url(' + currentUser.avatar + ')';
            avEl.textContent = '';
        } else {
            avEl.textContent = (currentUser.username || '?')[0].toUpperCase();
        }
    }
}

// === MY ACCOUNT ===
function loadDashAccount() {
    if (!currentUser) return;
    const el = id => document.getElementById(id);
    if (el('dashEditUsername')) el('dashEditUsername').value = currentUser.username || '';
    if (el('dashEditEmail')) el('dashEditEmail').value = currentUser.email || '';
    if (el('dashEditBio')) el('dashEditBio').value = currentUser.bio || '';
    if (el('dashEditWebsite')) el('dashEditWebsite').value = currentUser.website || '';
    if (el('dashEditLocation')) el('dashEditLocation').value = currentUser.location || '';
    if (el('dashEditJabber')) el('dashEditJabber').value = currentUser.jabber || '';
    if (el('dashStatCredits')) el('dashStatCredits').textContent = currentUser.credits || 0;
    if (el('dashStatRep')) el('dashStatRep').textContent = currentUser.reputationScore || 0;
    const avBig = el('dashAvatarBig');
    if (avBig) {
        if (currentUser.avatar) {
            avBig.style.backgroundImage = 'url(' + currentUser.avatar + ')';
            avBig.textContent = '';
        } else {
            avBig.textContent = (currentUser.username || '?')[0].toUpperCase();
        }
    }
}

async function saveDashAccountInfo(e) {
    e.preventDefault();
    const msgEl = document.getElementById('dashAccountMsg');
    if (!currentUser?.id) return showToast('Not logged in', 'error');
    const body = {
        bio: document.getElementById('dashEditBio').value.trim(),
        website: document.getElementById('dashEditWebsite').value.trim(),
        location: document.getElementById('dashEditLocation').value.trim(),
        jabber: document.getElementById('dashEditJabber').value.trim()
    };
    try {
        await api('/users/' + currentUser.id + '/profile', { method: 'PUT', body: JSON.stringify(body) });
        if (msgEl) { msgEl.textContent = 'Saved!'; msgEl.style.color = '#0f0'; }
        showToast('Profile updated!', 'success');
        await refreshMe();
        loadDashSidebar();
    } catch (err) {
        const msg = err.data?.error || 'Error saving profile';
        if (msgEl) { msgEl.textContent = msg; msgEl.style.color = '#f44'; }
        showToast(msg, 'error');
    }
}

// === INTERNAL WALLET ===
async function loadDashWallet() {
    const ov = document.getElementById('dashWalletOverview');
    const tx = document.getElementById('dashTxList');
    if (ov) ov.innerHTML = '<div class="loading">Loading wallets...</div>';
    try {
        const wallets = await api('/wallet');
        dashWallets = {};
        // Init any missing wallets silently
        const existing = new Set((wallets || []).map(w => w.currency));
        for (const c of ['BTC','ETH','USDT','LTC','XMR']) {
            if (!existing.has(c)) {
                try { await api('/wallet/init?currency=' + c, { method: 'POST' }); } catch {}
            }
        }
        if (!existing.size) {
            const w2 = await api('/wallet');
            (w2 || []).forEach(w => dashWallets[w.currency] = w);
        } else {
            (wallets || []).forEach(w => dashWallets[w.currency] = w);
        }
        renderDashWalletOverview(ov);
    } catch (err) {
        if (ov) ov.innerHTML = '<div class="dash-muted">Error loading wallets</div>';
    }
    // Load transactions
    try {
        const txs = await api('/wallet/transactions');
        renderDashTxList(tx, txs);
    } catch {
        if (tx) tx.innerHTML = '<div class="dash-muted">No transactions</div>';
    }
}

async function loadDashWalletQuiet() {
    try {
        const wallets = await api('/wallet');
        dashWallets = {};
        (wallets || []).forEach(w => dashWallets[w.currency] = w);
    } catch {}
}

function renderDashWalletOverview(container) {
    if (!container) return;
    const currencies = ['BTC','ETH','USDT','LTC','XMR'];
    const icons = { BTC:'&#x20BF;', ETH:'\u039E', USDT:'$', LTC:'\u0141', XMR:'\u2694' };
    container.innerHTML = '<div class="dash-wallet-grid">' +
        currencies.map(c => {
            const w = dashWallets[c];
            const bal = w ? parseFloat(w.balance || 0).toFixed(8) : '0.00000000';
            return '<div class="dash-wallet-card">' +
                '<div class="dash-wallet-icon">' + (icons[c]||c) + '</div>' +
                '<div class="dash-wallet-cur">' + c + '</div>' +
                '<div class="dash-wallet-bal">' + bal + '</div>' +
            '</div>';
        }).join('') +
    '</div>';
}

function renderDashTxList(container, txs) {
    if (!container) return;
    if (!txs || !txs.length) {
        container.innerHTML = '<div class="dash-muted">No transactions yet</div>';
        return;
    }
    container.innerHTML = txs.slice(0, 20).map(t => {
        const cls = t.type === 'deposit' ? 'tx-in' : 'tx-out';
        return '<div class="dash-tx-item ' + cls + '">' +
            '<span class="dash-tx-type">' + (t.type === 'deposit' ? '&#x2B06;' : '&#x2B07;') + ' ' + t.type + '</span>' +
            '<span class="dash-tx-amount">' + parseFloat(t.amount).toFixed(8) + ' ' + escapeHtml(t.currency) + '</span>' +
            '<span class="dash-tx-date">' + new Date(t.createdAt).toLocaleDateString() + '</span>' +
        '</div>';
    }).join('');
}

// === DEPOSIT ===
async function dashDoDeposit(e) {
    e.preventDefault();
    const currency = document.getElementById('dashDepositCurrency').value;
    const amount = parseFloat(document.getElementById('dashDepositAmount').value);
    const txId = document.getElementById('dashDepositTxId').value.trim();
    if (!amount || amount <= 0) return showToast('Invalid amount', 'error');
    if (!txId) return showToast('TX ID is required', 'error');
    try {
        await api('/wallet/deposit', {
            method: 'POST',
            body: JSON.stringify({ currency, amount, txId })
        });
        showToast('Deposit submitted for ' + amount + ' ' + currency, 'success');
        document.getElementById('dashDepositForm').reset();
    } catch (err) {
        showToast(err.data?.error || 'Deposit failed', 'error');
    }
}

// === WITHDRAW ===
function updateWithdrawBalance() {
    const cur = document.getElementById('dashWithdrawCurrency')?.value;
    const el = document.getElementById('dashWithdrawBalance');
    if (!el || !cur) return;
    const w = dashWallets[cur];
    el.textContent = w ? parseFloat(w.balance || 0).toFixed(8) + ' ' + cur : '0.00000000 ' + cur;
}

async function dashDoWithdraw(e) {
    e.preventDefault();
    const currency = document.getElementById('dashWithdrawCurrency').value;
    const amount = parseFloat(document.getElementById('dashWithdrawAmount').value);
    const walletAddress = document.getElementById('dashWithdrawAddress').value.trim();
    if (!amount || amount <= 0) return showToast('Invalid amount', 'error');
    if (!walletAddress) return showToast('Wallet address required', 'error');
    try {
        await api('/wallet/withdraw', {
            method: 'POST',
            body: JSON.stringify({ currency, amount, walletAddress })
        });
        showToast('Withdrawal of ' + amount + ' ' + currency + ' submitted', 'success');
        document.getElementById('dashWithdrawForm').reset();
        loadDashWalletQuiet();
        updateWithdrawBalance();
    } catch (err) {
        showToast(err.data?.error || 'Withdrawal failed', 'error');
    }
}

// === ORDERS ===
async function loadDashOrders(type) {
    const list = document.getElementById('dashOrdersList');
    if (!list) return;
    // update tabs
    document.querySelectorAll('.dash-tabs .dash-tab').forEach(t => t.classList.remove('active'));
    const activeTab = Array.from(document.querySelectorAll('.dash-tabs .dash-tab')).find(t => t.textContent.toLowerCase().includes(type));
    if (activeTab) activeTab.classList.add('active');
    list.innerHTML = '<div class="loading">Loading...</div>';
    try {
        const orders = await api('/orders/' + type);
        if (!orders || !orders.length) {
            list.innerHTML = '<div class="dash-muted">No ' + type + ' orders</div>';
            return;
        }
        list.innerHTML = orders.map(o =>
            '<div class="dash-order-item" onclick="navigate(\'orderDetail\', ' + o.id + ')">' +
                '<div class="dash-order-title">' + escapeHtml(o.listingTitle || 'Order #' + o.id) + '</div>' +
                '<div class="dash-order-meta">' +
                    '<span class="dash-order-status status-' + (o.status||'').toLowerCase() + '">' + escapeHtml(o.status) + '</span>' +
                    '<span>' + parseFloat(o.totalPrice || 0).toFixed(8) + ' ' + escapeHtml(o.currency || 'BTC') + '</span>' +
                    '<span>' + new Date(o.createdAt).toLocaleDateString() + '</span>' +
                '</div>' +
            '</div>'
        ).join('');
    } catch {
        list.innerHTML = '<div class="dash-muted">Error loading orders</div>';
    }
}

// === MESSENGER ===
async function loadDashMessenger() {
    const el = document.getElementById('dashMessengerContent');
    if (!el) return;
    el.innerHTML = '<div class="loading">Loading conversations...</div>';
    try {
        const convs = await api('/messages/conversations');
        if (!convs || !convs.length) {
            el.innerHTML = '<div class="dash-muted">No conversations yet</div>';
            return;
        }
        el.innerHTML = convs.map(c =>
            '<div class="dash-conv-item" onclick="navigate(\'messages\', {userId:' + c.userId + '})">' +
                '<div class="dash-conv-user">' + escapeHtml(c.username || 'User') + '</div>' +
                '<div class="dash-conv-preview">' + escapeHtml((c.lastMessage || '').substring(0, 80)) + '</div>' +
                '<div class="dash-conv-date">' + new Date(c.lastMessageAt).toLocaleDateString() + '</div>' +
            '</div>'
        ).join('');
    } catch {
        el.innerHTML = '<div class="dash-muted">Error loading messages</div>';
    }
}

// === TICKETS ===
async function loadDashTickets() {
    const el = document.getElementById('dashTicketsList');
    if (!el) return;
    el.innerHTML = '<div class="loading">Loading tickets...</div>';
    try {
        const tickets = await api('/tickets/my');
        if (!tickets || !tickets.length) {
            el.innerHTML = '<div class="dash-muted">No tickets. Click "+ New Ticket" to create one.</div>';
            return;
        }
        el.innerHTML = tickets.map(t =>
            '<div class="dash-ticket-item" onclick="loadDashTicketDetail(' + t.id + ')">' +
                '<div class="dash-ticket-subject">' + escapeHtml(t.subject) + '</div>' +
                '<div class="dash-ticket-meta">' +
                    '<span class="dash-ticket-status status-' + (t.status||'').toLowerCase() + '">' + escapeHtml(t.status) + '</span>' +
                    '<span>' + new Date(t.createdAt).toLocaleDateString() + '</span>' +
                '</div>' +
            '</div>'
        ).join('');
    } catch {
        el.innerHTML = '<div class="dash-muted">Error loading tickets</div>';
    }
}

function showDashNewTicketForm() {
    const el = document.getElementById('dashTicketsList');
    if (!el) return;
    el.innerHTML =
        '<div class="dash-card">' +
            '<h3>New Support Ticket</h3>' +
            '<form onsubmit="submitDashTicket(event)">' +
                '<div class="input-group"><label>Subject</label><input type="text" id="dashTicketSubject" required maxlength="200"></div>' +
                '<div class="input-group"><label>Message</label><textarea id="dashTicketMessage" rows="5" required maxlength="2000"></textarea></div>' +
                '<div class="dash-form-actions">' +
                    '<button type="submit" class="btn btn-primary">Submit Ticket</button>' +
                    '<button type="button" class="btn btn-outline" onclick="loadDashTickets()">Cancel</button>' +
                '</div>' +
            '</form>' +
        '</div>';
}

async function submitDashTicket(e) {
    e.preventDefault();
    const subject = document.getElementById('dashTicketSubject').value.trim();
    const message = document.getElementById('dashTicketMessage').value.trim();
    if (!subject || !message) return showToast('Fill in all fields', 'error');
    try {
        await api('/tickets', { method: 'POST', body: JSON.stringify({ subject, message }) });
        showToast('Ticket submitted!', 'success');
        loadDashTickets();
    } catch (err) {
        showToast(err.data?.error || 'Error creating ticket', 'error');
    }
}

async function loadDashTicketDetail(id) {
    const el = document.getElementById('dashTicketsList');
    if (!el) return;
    el.innerHTML = '<div class="loading">Loading ticket...</div>';
    try {
        const t = await api('/tickets/' + id);
        let html = '<div class="dash-card">' +
            '<div class="dash-ticket-detail-header">' +
                '<button class="btn btn-outline btn-sm" onclick="loadDashTickets()">&larr; Back</button>' +
                '<h3>' + escapeHtml(t.subject) + '</h3>' +
                '<span class="dash-ticket-status status-' + (t.status||'').toLowerCase() + '">' + escapeHtml(t.status) + '</span>' +
            '</div>' +
            '<div class="dash-ticket-messages">';
        if (t.replies && t.replies.length) {
            html += t.replies.map(r =>
                '<div class="dash-ticket-msg ' + (r.isAdmin ? 'msg-admin' : 'msg-user') + '">' +
                    '<div class="dash-ticket-msg-header"><strong>' + escapeHtml(r.author || (r.isAdmin ? 'Support' : 'You')) + '</strong> <span>' + new Date(r.createdAt).toLocaleString() + '</span></div>' +
                    '<div class="dash-ticket-msg-body">' + escapeHtml(r.message) + '</div>' +
                '</div>'
            ).join('');
        } else {
            html += '<div class="dash-ticket-msg msg-user"><div class="dash-ticket-msg-body">' + escapeHtml(t.message || '') + '</div></div>';
        }
        html += '</div>';
        if (t.status !== 'closed') {
            html += '<form onsubmit="replyDashTicket(event, ' + id + ')" class="dash-ticket-reply">' +
                '<textarea id="dashTicketReply" rows="3" required placeholder="Type your reply..."></textarea>' +
                '<div class="dash-form-actions">' +
                    '<button type="submit" class="btn btn-primary btn-sm">Reply</button>' +
                    '<button type="button" class="btn btn-danger btn-sm" onclick="closeDashTicket(' + id + ')">Close Ticket</button>' +
                '</div>' +
            '</form>';
        }
        html += '</div>';
        el.innerHTML = html;
    } catch {
        el.innerHTML = '<div class="dash-muted">Error loading ticket</div>';
    }
}

async function replyDashTicket(e, id) {
    e.preventDefault();
    const message = document.getElementById('dashTicketReply').value.trim();
    if (!message) return;
    try {
        await api('/tickets/' + id + '/reply', { method: 'POST', body: JSON.stringify({ message }) });
        showToast('Reply sent', 'success');
        loadDashTicketDetail(id);
    } catch (err) {
        showToast(err.data?.error || 'Error sending reply', 'error');
    }
}

async function closeDashTicket(id) {
    try {
        await api('/tickets/' + id + '/close', { method: 'PUT' });
        showToast('Ticket closed', 'success');
        loadDashTickets();
    } catch (err) {
        showToast(err.data?.error || 'Error closing ticket', 'error');
    }
}

// === BLOCK USERS ===
async function loadDashBlockedUsers() {
    const el = document.getElementById('dashBlockedList');
    if (!el) return;
    try {
        const blocked = await api('/users/blocked');
        if (!blocked || !blocked.length) {
            el.innerHTML = '<p class="dash-muted">No blocked users</p>';
            return;
        }
        el.innerHTML = blocked.map(u =>
            '<div class="dash-blocked-item">' +
                '<span>' + escapeHtml(u.username) + '</span>' +
                '<button class="btn btn-outline btn-sm" onclick="dashUnblockUser(\'' + escapeHtml(u.username) + '\')">Unblock</button>' +
            '</div>'
        ).join('');
    } catch {
        el.innerHTML = '<p class="dash-muted">No blocked users</p>';
    }
}

async function dashBlockUser() {
    const input = document.getElementById('dashBlockUsername');
    const username = input?.value.trim();
    if (!username) return showToast('Enter a username', 'error');
    try {
        await api('/users/block', { method: 'POST', body: JSON.stringify({ username }) });
        showToast(username + ' blocked', 'success');
        input.value = '';
        loadDashBlockedUsers();
    } catch (err) {
        showToast(err.data?.error || 'Error blocking user', 'error');
    }
}

async function dashUnblockUser(username) {
    try {
        await api('/users/unblock', { method: 'POST', body: JSON.stringify({ username }) });
        showToast(username + ' unblocked', 'success');
        loadDashBlockedUsers();
    } catch (err) {
        showToast(err.data?.error || 'Error unblocking user', 'error');
    }
}

const API = '/api';
let currentUser = null;
let currentPage = 1;
let badgeInterval = null;
let heartbeatInterval = null;
let captchaStore = {};

// === Captcha Anti-Bot System ===
function generateCaptcha(formId) {
    const ops = ['+', '-', '×'];
    const op = ops[Math.floor(Math.random() * ops.length)];
    let a, b, answer;
    switch (op) {
        case '+':
            a = Math.floor(Math.random() * 40) + 5;
            b = Math.floor(Math.random() * 40) + 5;
            answer = a + b;
            break;
        case '-':
            a = Math.floor(Math.random() * 40) + 20;
            b = Math.floor(Math.random() * 20) + 1;
            answer = a - b;
            break;
        case '×':
            a = Math.floor(Math.random() * 12) + 2;
            b = Math.floor(Math.random() * 10) + 2;
            answer = a * b;
            break;
    }
    captchaStore[formId] = answer;
    const el = document.getElementById(formId + 'CaptchaChallenge');
    if (el) {
        el.innerHTML = `<span class="captcha-question">Quanto fa <strong>${a} ${op} ${b}</strong> ?</span>`;
    }
    const input = document.getElementById(formId + 'CaptchaAnswer');
    if (input) input.value = '';
}

function verifyCaptcha(formId) {
    const input = document.getElementById(formId + 'CaptchaAnswer');
    if (!input) return false;
    return parseInt(input.value, 10) === captchaStore[formId];
}

// === Offshore & Anti-Fingerprint Protection ===
(function initOffshoreShield() {
    // Block WebRTC IP leak (disables RTCPeerConnection to prevent real IP exposure)
    if (window.RTCPeerConnection) {
        window.RTCPeerConnection = undefined;
    }
    if (window.webkitRTCPeerConnection) {
        window.webkitRTCPeerConnection = undefined;
    }
    if (window.mozRTCPeerConnection) {
        window.mozRTCPeerConnection = undefined;
    }

    // Disable battery API (fingerprinting vector)
    if (navigator.getBattery) {
        navigator.getBattery = undefined;
    }

    // Disable device orientation/motion (fingerprinting vector)
    window.addEventListener('deviceorientation', e => e.stopImmediatePropagation(), true);
    window.addEventListener('devicemotion', e => e.stopImmediatePropagation(), true);

    // Block Geolocation API
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition = (s, e) => e?.({ code: 1, message: 'Blocked by CrimeMarket Shield' });
        navigator.geolocation.watchPosition = (s, e) => e?.({ code: 1, message: 'Blocked by CrimeMarket Shield' });
    }

    // Anti-clipboard sniffing — prevent external paste read
    document.addEventListener('copy', e => {
        if (e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
            e.preventDefault();
        }
    });

    console.log('%c🛡️ CrimeMarket Offshore Shield Active', 'color: #00e5a0; font-size: 14px; font-weight: bold;');
    console.log('%cAnti-DDoS • Security Headers • WebRTC Block • No-Track • Anti-Fingerprint', 'color: #6b7fa0; font-size: 11px;');
})();

// === Init ===
document.addEventListener('DOMContentLoaded', () => {
    // Telegram Mini App detection & theme
    initTelegram();

    const saved = localStorage.getItem('crimecode_user');
    if (saved) {
        try {
            currentUser = JSON.parse(saved);
            refreshMe();
        } catch { localStorage.removeItem('crimecode_user'); }
    }
    // Auto-login via Telegram if inside Mini App
    if (!currentUser && window.Telegram?.WebApp?.initData) {
        telegramAutoLogin();
    }
    updateAuthUI();
    loadMarketplaceStats();

    // Show landing or marketplace based on auth
    if (currentUser) {
        navigate('marketplace');
    } else {
        showLanding();
    }
});

// === Telegram Mini App ===
function initTelegram() {
    const tg = window.Telegram?.WebApp;
    if (!tg) return;
    tg.ready();
    tg.expand();
    // Apply Telegram theme colors to CSS variables
    applyTelegramTheme(tg.themeParams);
    tg.onEvent('themeChanged', () => applyTelegramTheme(tg.themeParams));
    document.body.classList.add('tg-mini-app');
}

function applyTelegramTheme(tp) {
    if (!tp) return;
    const root = document.documentElement.style;
    if (tp.bg_color) root.setProperty('--bg-primary', tp.bg_color);
    if (tp.secondary_bg_color) root.setProperty('--bg-secondary', tp.secondary_bg_color);
    if (tp.text_color) root.setProperty('--text-primary', tp.text_color);
    if (tp.hint_color) root.setProperty('--text-secondary', tp.hint_color);
    if (tp.link_color) root.setProperty('--accent-cyan', tp.link_color);
    if (tp.button_color) root.setProperty('--accent', tp.button_color);
    if (tp.button_text_color) root.setProperty('--btn-text', tp.button_text_color);
    if (tp.section_bg_color) root.setProperty('--bg-card', tp.section_bg_color);
}

async function telegramAutoLogin() {
    try {
        const data = await api('/auth/telegram', {
            method: 'POST',
            body: JSON.stringify({ initData: window.Telegram.WebApp.initData })
        });
        if (data?.token) {
            currentUser = { token: data.token, userId: data.userId, username: data.username };
            localStorage.setItem('crimecode_user', JSON.stringify(currentUser));
            refreshMe();
        }
    } catch (e) {
        console.warn('Telegram auto-login failed:', e);
    }
}

// === API Helper ===
async function api(path, options = {}) {
    const headers = { 'Content-Type': 'application/json', ...options.headers };
    if (currentUser?.token) headers['Authorization'] = `Bearer ${currentUser.token}`;
    const res = await fetch(`${API}${path}`, { ...options, headers });
    if (res.status === 204) return null;
    const data = await res.json().catch(() => null);
    if (!res.ok) throw { status: res.status, data };
    return data;
}

async function apiRaw(path, options = {}) {
    const headers = { ...options.headers };
    if (currentUser?.token) headers['Authorization'] = `Bearer ${currentUser.token}`;
    const res = await fetch(`${API}${path}`, { ...options, headers });
    if (res.status === 204) return null;
    const data = await res.json().catch(() => null);
    if (!res.ok) throw { status: res.status, data };
    return data;
}

// === Refresh /me ===
async function refreshMe() {
    try {
        const me = await api('/auth/me');
        currentUser = { ...currentUser, ...me };
        localStorage.setItem('crimecode_user', JSON.stringify(currentUser));
        updateAuthUI();
        startBadgePolling();
        startHeartbeat();
    } catch {
        logout();
    }
}

// === Navigation ===
function navigate(page, params = {}) {
    // Require authentication — redirect to landing if not logged in
    if (!currentUser && page !== 'landing') {
        showLanding();
        return;
    }
    document.querySelectorAll('#mainContent > div').forEach(d => {
        d.style.display = 'none';
        d.classList.remove('page-enter');
    });
    // Update nav links
    document.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));
    const navLink = document.querySelector(`.nav-link[data-page="${page}"]`);
    if (navLink) navLink.classList.add('active');

    switch (page) {
        case 'home':
            document.getElementById('pageMarketplace').style.display = '';
            loadMarketplace();
            break;
        case 'dashboard':
            document.getElementById('pageDashboard').style.display = '';
            initDashboard();
            break;
        case 'profile':
            document.getElementById('pageProfile').style.display = '';
            loadProfile(params.id);
            break;
        case 'notifications':
            document.getElementById('pageNotifications').style.display = '';
            loadNotifications();
            break;
        case 'messages':
            document.getElementById('pageMessages').style.display = '';
            if (params.userId) loadConversation(params.userId);
            else loadConversations();
            break;
        case 'marketplace':
            document.getElementById('pageMarketplace').style.display = '';
            loadMarketplace();
            break;
        case 'listingDetail':
            document.getElementById('pageListingDetail').style.display = '';
            loadListingDetail(params.id);
            break;
        case 'vendors':
            document.getElementById('pageVendors').style.display = '';
            loadVendors();
            break;
        case 'vendorProfile':
            document.getElementById('pageVendorProfile').style.display = '';
            loadVendorProfile(params.id);
            break;
        case 'myListings':
            document.getElementById('pageMyListings').style.display = '';
            loadMyListings();
            break;
        case 'myOrders':
            document.getElementById('pageMyOrders').style.display = '';
            loadMyOrders('buying');
            break;
        case 'orderDetail':
            document.getElementById('pageOrderDetail').style.display = '';
            loadOrderDetail(params.id);
            break;
        case 'leaderboard':
            document.getElementById('pageLeaderboard').style.display = '';
            loadLeaderboard('rating');
            break;
        case 'members':
            document.getElementById('pageMembers').style.display = '';
            loadOnlineUsers();
            break;
        case 'admin':
            document.getElementById('pageAdmin').style.display = '';
            loadAdmin();
            break;
        case 'wallet':
            document.getElementById('pageWallet').style.display = '';
            loadWallet();
            break;
        case 'wishlist':
            document.getElementById('pageWishlist').style.display = '';
            loadWishlist();
            break;
        case 'vouchers':
            document.getElementById('pageVouchers').style.display = '';
            loadMyVouchers();
            break;
        case 'vendorStats':
            document.getElementById('pageVendorStats').style.display = '';
            loadVendorStats();
            break;
        case 'settings2fa':
            document.getElementById('page2FA').style.display = '';
            load2FASettings();
            break;
        case 'tickets':
            document.getElementById('pageTickets').style.display = '';
            loadTickets();
            break;
        case 'ticketDetail':
            document.getElementById('pageTicketDetail').style.display = '';
            loadTicketDetail(params.id);
            break;
    }
    closeUserMenu();
    window.scrollTo(0, 0);
    // Animate page entrance
    const visiblePage = document.querySelector('#mainContent > div[style*="display: block"], #mainContent > div:not([style*="display: none"]):not([style*="display:none"])');
    if (visiblePage) {
        visiblePage.classList.add('page-enter');
    }
}

function showLanding() {
    document.querySelectorAll('#mainContent > div').forEach(d => {
        d.style.display = 'none';
        d.classList.remove('page-enter');
    });
    document.getElementById('pageLanding').style.display = '';
    // Show landing stats
    loadLandingStats();
}

async function loadLandingStats() {
    try {
        const s = await api('/leaderboard/stats');
        const el = document.getElementById('landingStats');
        if (el) el.innerHTML = `
            <div class="landing-stat"><span class="landing-stat-num">${s.totalUsers}</span><span>Utenti</span></div>
            <div class="landing-stat"><span class="landing-stat-num">${s.totalListings}</span><span>Annunci</span></div>
            <div class="landing-stat"><span class="landing-stat-num">${s.totalSales}</span><span>Vendite</span></div>
            <div class="landing-stat"><span class="landing-stat-num">${s.onlineUsers}</span><span>Online</span></div>`;
    } catch { /* ignore */ }
}

// === Auth ===
function updateAuthUI() {
    const nav = document.getElementById('headerNav');
    const navLogged = document.getElementById('headerNavLogged');
    if (currentUser) {
        if (nav) nav.style.display = 'none';
        if (navLogged) navLogged.style.display = 'flex';
        // Show header for logged-in users, hide landing buttons
        const header = document.querySelector('.header-nexus');
        if (header) header.style.display = '';
        const userMenu = document.getElementById('userMenu');
        if (userMenu) userMenu.style.display = '';
        document.getElementById('userGreeting').textContent = currentUser.username || currentUser.Username || 'User';
        // Credits
        const cc = document.getElementById('creditCount');
        if (cc) cc.textContent = currentUser.credits ?? 0;
        // Avatar
        const avatarEl = document.getElementById('userMenuAvatar');
        if (avatarEl) {
            if (currentUser.avatarUrl) {
                avatarEl.innerHTML = `<img src="${escapeHtml(currentUser.avatarUrl)}" alt="">`;
            } else {
                avatarEl.textContent = (currentUser.username || '?').charAt(0).toUpperCase();
            }
        }
        // Role / Admin link
        try {
            const payload = JSON.parse(atob(currentUser.token.split('.')[1]));
            const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
            currentUser.role = role;
            const adminLink = document.getElementById('adminLink');
            if (adminLink) adminLink.style.display = (role === 'Admin' || role === 'Moderator') ? '' : 'none';
        } catch { /* ignore */ }
        // Show marketplace buttons based on vendor status
        const nlb = document.getElementById('newListingBtn');
        const vendorApplyBtn = document.getElementById('vendorApplyBtn');
        const myListingsBtn = document.getElementById('myListingsBtn');
        const myOrdersBtn = document.getElementById('myOrdersBtn');
        const vendorPanelLink = document.getElementById('vendorPanelLink');
        const vendorVouchersLink = document.getElementById('vendorVouchersLink');
        const vendorStatsLink = document.getElementById('vendorStatsLink');
        if (myOrdersBtn) myOrdersBtn.style.display = '';
        
        // Check vendor status
        checkVendorStatus().then(isVendor => {
            if (nlb) nlb.style.display = isVendor ? '' : 'none';
            if (vendorApplyBtn) vendorApplyBtn.style.display = isVendor ? 'none' : '';
            if (myListingsBtn) myListingsBtn.style.display = isVendor ? '' : 'none';
            if (vendorPanelLink) vendorPanelLink.style.display = isVendor ? '' : 'none';
            if (vendorVouchersLink) vendorVouchersLink.style.display = isVendor ? '' : 'none';
            if (vendorStatsLink) vendorStatsLink.style.display = isVendor ? '' : 'none';
        });
        updateChatVisibility();
    } else {
        if (nav) nav.style.display = 'flex';
        if (navLogged) navLogged.style.display = 'none';
        const nlb = document.getElementById('newListingBtn');
        if (nlb) nlb.style.display = 'none';
        const vendorApplyBtn = document.getElementById('vendorApplyBtn');
        if (vendorApplyBtn) vendorApplyBtn.style.display = 'none';
        const myListingsBtn = document.getElementById('myListingsBtn');
        if (myListingsBtn) myListingsBtn.style.display = 'none';
        const myOrdersBtn = document.getElementById('myOrdersBtn');
        if (myOrdersBtn) myOrdersBtn.style.display = 'none';
        updateChatVisibility();
    }
}

async function register(e) {
    e.preventDefault();
    const errEl = document.getElementById('registerError');
    const btn = e.target.querySelector('button[type="submit"]');
    errEl.textContent = '';
    if (!verifyCaptcha('register')) {
        errEl.textContent = 'Captcha errato. Riprova.';
        generateCaptcha('register');
        return;
    }
    const origText = btn.textContent;
    btn.textContent = 'Creazione...';
    btn.disabled = true;
    try {
        const data = await api('/auth/register', {
            method: 'POST',
            body: JSON.stringify({
                username: document.getElementById('regUsername').value.trim(),
                email: document.getElementById('regEmail').value.trim(),
                password: document.getElementById('regPassword').value
            })
        });
        currentUser = data;
        localStorage.setItem('crimecode_user', JSON.stringify(data));
        refreshMe();
        closeModal();
        showToast('Account creato! Benvenuto su CrimeMarket', 'success');
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore durante la registrazione. Riprova.';
    } finally {
        btn.textContent = origText;
        btn.disabled = false;
    }
}

async function login(e) {
    e.preventDefault();
    const errEl = document.getElementById('loginError');
    const btn = e.target.querySelector('button[type="submit"]');
    errEl.textContent = '';
    if (!verifyCaptcha('login')) {
        errEl.textContent = 'Captcha errato. Riprova.';
        generateCaptcha('login');
        return;
    }
    const origText = btn.textContent;
    btn.textContent = 'Accesso...';
    btn.disabled = true;
    try {
        const body = {
            email: document.getElementById('loginEmail').value.trim(),
            password: document.getElementById('loginPassword').value,
            totpCode: document.getElementById('login2FACode')?.value?.trim() || null
        };
        const data = await api('/auth/login', {
            method: 'POST',
            body: JSON.stringify(body)
        });
        currentUser = data;
        localStorage.setItem('crimecode_user', JSON.stringify(data));
        refreshMe();
        closeModal();
        navigate('marketplace');
    } catch (err) {
        if (err.data?.requires2FA) {
            errEl.textContent = err.data.error || 'Inserisci il codice 2FA';
            const grp = document.getElementById('login2FAGroup');
            if (grp) grp.style.display = 'block';
        } else {
            errEl.textContent = err.data?.error || 'Email o password non validi';
        }
    } finally {
        btn.textContent = origText;
        btn.disabled = false;
    }
}

function logout() {
    currentUser = null;
    localStorage.removeItem('crimecode_user');
    updateAuthUI();
    stopBadgePolling();
    stopHeartbeat();
    showLanding();
}

// === User Menu ===
function toggleUserMenu() {
    const dd = document.getElementById('userDropdown');
    dd.classList.toggle('show');
    closeWalletDropdown();
}
function closeUserMenu() {
    const dd = document.getElementById('userDropdown');
    if (dd) dd.classList.remove('show');
}

// === Wallet Dropdown ===
function toggleWalletDropdown() {
    const menu = document.getElementById('walletMenu');
    if (menu) menu.classList.toggle('show');
    closeUserMenu();
}
function closeWalletDropdown() {
    const menu = document.getElementById('walletMenu');
    if (menu) menu.classList.remove('show');
}
function connectWallet(provider) {
    showToast(`Connessione wallet ${provider} in arrivo...`, 'info');
    closeWalletDropdown();
}
function disconnectWallet() {
    document.getElementById('walletStatus').textContent = 'Nessun wallet collegato';
    const bal = document.getElementById('walletBalance');
    if (bal) bal.style.display = 'none';
    const disc = document.getElementById('disconnectWalletBtn');
    if (disc) disc.style.display = 'none';
    showToast('Wallet disconnesso', 'info');
    closeWalletDropdown();
}

document.addEventListener('click', (e) => {
    if (!e.target.closest('.user-menu')) closeUserMenu();
    if (!e.target.closest('.wallet-dropdown')) closeWalletDropdown();
});

// === Badge Polling ===
function startBadgePolling() {
    updateBadges();
    if (badgeInterval) clearInterval(badgeInterval);
    badgeInterval = setInterval(updateBadges, 30000);
}
function stopBadgePolling() {
    if (badgeInterval) { clearInterval(badgeInterval); badgeInterval = null; }
    setBadge('notifBadge', 0);
    setBadge('msgBadge', 0);
    setBadge('chatBadge', 0);
}
async function updateBadges() {
    if (!currentUser) return;
    try {
        const [n, m, c] = await Promise.all([
            api('/notifications/unread-count'),
            api('/messages/unread-count'),
            api('/chat/unread-count')
        ]);
        setBadge('notifBadge', n.count);
        setBadge('msgBadge', m.count);
        setBadge('chatBadge', c.count);
    } catch { /* ignore */ }
}
function setBadge(id, count) {
    const el = document.getElementById(id);
    if (!el) return;
    if (count > 0) {
        el.textContent = count > 99 ? '99+' : count;
        el.style.display = '';
    } else {
        el.style.display = 'none';
    }
}

// === Heartbeat ===
function startHeartbeat() {
    if (heartbeatInterval) clearInterval(heartbeatInterval);
    heartbeatInterval = setInterval(async () => {
        try { await api('/users/heartbeat', { method: 'POST' }); } catch { /* ignore */ }
    }, 60000);
}
function stopHeartbeat() {
    if (heartbeatInterval) { clearInterval(heartbeatInterval); heartbeatInterval = null; }
}

// === Marketplace Stats ===
async function loadMarketplaceStats() {
    try {
        const s = await api('/leaderboard/stats');
        const u = s.totalUsers || 0, l = s.totalListings || 0, v = s.totalSales || 0, o = s.onlineUsers || 0;
        const bar = document.getElementById('marketStatsBar');
        if (bar) bar.textContent = `${u} utenti · ${l} annunci · ${v} vendite`;
        const oc = document.getElementById('onlineCount');
        if (oc) oc.textContent = `${o} online`;
        const fs = document.getElementById('footerStats');
        if (fs) fs.textContent = `${u} utenti · ${l} annunci · ${v} vendite · ${o} online`;
    } catch { /* ignore */ }
}

// === Modal ===
function showModal(type, params = {}) {
    const overlay = document.getElementById('modalOverlay');
    overlay.querySelectorAll('[id^="modal"]').forEach(d => {
        if (d.id !== 'modalOverlay') d.style.display = 'none';
    });
    const map = {
        login: 'modalLogin', register: 'modalRegister',
        sendMessage: 'modalSendMessage', avatar: 'modalAvatar',
        newListing: 'modalNewListing', reputation: 'modalReputation',
        vendorApply: 'modalVendorApply', orderCreate: 'modalOrderCreate',
        createVoucher: 'modalCreateVoucher'
    };
    const el = document.getElementById(map[type]);
    if (el) el.style.display = '';
    overlay.classList.add('active');

    if (type === 'newListing') loadCategoriesSelect('listingCategory', true);
    if (type === 'sendMessage' && params.userId && params.username) {
        document.getElementById('msgReceiverId').value = params.userId;
        document.getElementById('msgReceiverName').textContent = `A: ${params.username}`;
    }
    if (type === 'reputation' && params.userId && params.username) {
        document.getElementById('repUserId').value = params.userId;
        document.getElementById('repUserName').textContent = params.username;
        document.getElementById('repPoints').value = 1;
        setRepPoints(1);
    }
    if (type === 'avatar') loadAvatarPreview();
    if (type === 'login') generateCaptcha('login');
    if (type === 'register') generateCaptcha('register');
}
function closeModal() {
    document.getElementById('modalOverlay').classList.remove('active');
}

// === Load Categories Select (for modals) ===
async function loadCategoriesSelect(selectId, marketplaceOnly) {
    const sel = document.getElementById(selectId);
    try {
        const cats = await api('/categories');
        let options = '<option value="">Seleziona categoria</option>';
        const addOptions = (list, indent = 0) => {
            for (const c of list) {
                if (marketplaceOnly && !c.isMarketplace) {
                    if (c.subCategories?.length) addOptions(c.subCategories, indent + 1);
                    continue;
                }
                const prefix = '—'.repeat(indent) + (indent ? ' ' : '');
                options += `<option value="${c.id}">${prefix}${c.icon} ${escapeHtml(c.name)}</option>`;
                if (c.subCategories?.length) addOptions(c.subCategories, indent + 1);
            }
        };
        addOptions(cats);
        sel.innerHTML = options;
    } catch { /* ignore */ }
}

function formatContent(text) {
    if (!text) return '';
    let s = escapeHtml(text);
    // BBCode
    s = s.replace(/\[b\]([\s\S]*?)\[\/b\]/gi, '<strong>$1</strong>');
    s = s.replace(/\[i\]([\s\S]*?)\[\/i\]/gi, '<em>$1</em>');
    s = s.replace(/\[u\]([\s\S]*?)\[\/u\]/gi, '<u>$1</u>');
    s = s.replace(/\[s\]([\s\S]*?)\[\/s\]/gi, '<s>$1</s>');
    s = s.replace(/\[color=([^\]]+)\]([\s\S]*?)\[\/color\]/gi, '<span style="color:$1">$2</span>');
    s = s.replace(/\[size=([^\]]+)\]([\s\S]*?)\[\/size\]/gi, '<span style="font-size:$1">$2</span>');
    s = s.replace(/\[url=([^\]]+)\]([\s\S]*?)\[\/url\]/gi, '<a href="$1" target="_blank" rel="noopener noreferrer">$2</a>');
    s = s.replace(/\[url\]([\s\S]*?)\[\/url\]/gi, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>');
    s = s.replace(/\[img\]([\s\S]*?)\[\/img\]/gi, '<img src="$1" class="post-image" alt="img" loading="lazy">');
    s = s.replace(/\[quote\]([\s\S]*?)\[\/quote\]/gi, '<blockquote class="bb-quote">$1</blockquote>');
    s = s.replace(/\[quote=([^\]]+)\]([\s\S]*?)\[\/quote\]/gi, '<blockquote class="bb-quote"><cite>$1:</cite>$2</blockquote>');
    s = s.replace(/\[code\]([\s\S]*?)\[\/code\]/gi, '<pre><code>$1</code></pre>');
    s = s.replace(/\[spoiler\]([\s\S]*?)\[\/spoiler\]/gi, '<details class="spoiler"><summary>Spoiler</summary>$1</details>');
    s = s.replace(/\[list\]([\s\S]*?)\[\/list\]/gi, (m, inner) => '<ul>' + inner.replace(/\[\*\]/g, '<li>') + '</ul>');
    // Markdown — Code blocks
    s = s.replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>');
    // Inline code
    s = s.replace(/`([^`]+)`/g, '<code>$1</code>');
    // Bold
    s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    // Italic
    s = s.replace(/\*(.+?)\*/g, '<em>$1</em>');
    // Strikethrough
    s = s.replace(/~~(.+?)~~/g, '<s>$1</s>');
    // Blockquote
    s = s.replace(/^&gt; (.+)$/gm, '<blockquote>$1</blockquote>');
    // Line breaks
    s = s.replace(/\n/g, '<br>');
    return s;
}

// === Search Marketplace ===
async function searchMarketplace() {
    const q = document.getElementById('searchInput').value.trim();
    if (!q || q.length < 2) return;
    marketSearch = q;
    marketPage = 1;
    navigate('marketplace');
}

// === Profile ===
async function loadProfile(id) {
    const container = document.getElementById('profileDetail');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const [u, repHistory] = await Promise.all([
            api(`/users/${id}`),
            api(`/reputation/${id}`)
        ]);
        const isOwnProfile = currentUser && currentUser.userId === u.id;
        const avatarHtml = u.avatarUrl
            ? `<img src="${escapeHtml(u.avatarUrl)}" alt="">`
            : u.username.charAt(0).toUpperCase();

        const statusIcon = {online:'🟢',away:'🟡',busy:'🔴',offline:'⚫'}[u.status] || '⚫';

        let html = `
            <span style="display:inline-block;margin-bottom:0.8rem;cursor:pointer;color:var(--accent);font-size:13px" onclick="navigate('marketplace')">← Torna al marketplace</span>
            <div class="profile-header">
                <div class="profile-avatar">${avatarHtml}</div>
                <div class="profile-info">
                    <h2>${escapeHtml(u.username)} <span class="status-indicator" title="${u.status || 'offline'}">${statusIcon}</span></h2>
                    ${u.customTitle ? `<div class="profile-custom-title">${escapeHtml(u.customTitle)}</div>` : ''}
                    <div class="profile-stats-row">
                        <span>⭐ <strong>${u.reputationScore}</strong> rep</span>
                        <span>💰 <strong>${u.credits}</strong> crediti</span>
                        <span>👥 <strong>${u.followerCount}</strong> follower</span>
                        <span>👤 <strong>${u.followingCount}</strong> seguiti</span>
                        <span>📅 Membro dal ${new Date(u.createdAt).toLocaleDateString('it-IT')}</span>
                    </div>
                    <div class="profile-actions">
                        ${isOwnProfile ? `<button class="btn btn-outline btn-sm" onclick="showModal('avatar')">📷 Avatar</button>
                            <button class="btn btn-outline btn-sm" onclick="toggleProfileEdit(${u.id})">✏️ Modifica</button>
                            <select class="status-select" onchange="updateUserStatus(this.value)">
                                <option value="online" ${u.status==='online'?'selected':''}>🟢 Online</option>
                                <option value="away" ${u.status==='away'?'selected':''}>🟡 Away</option>
                                <option value="busy" ${u.status==='busy'?'selected':''}>🔴 Busy</option>
                                <option value="offline" ${u.status==='offline'?'selected':''}>⚫ Offline</option>
                            </select>` : ''}
                        ${currentUser && !isOwnProfile ? `
                            <button class="btn btn-primary btn-sm" onclick="showModal('sendMessage',{userId:${u.id},username:'${escapeHtml(u.username).replace(/'/g, "\\'")}'})">✉️ Messaggio</button>
                            <button class="btn btn-outline btn-sm" onclick="showModal('reputation',{userId:${u.id},username:'${escapeHtml(u.username).replace(/'/g, "\\'")}'})">⭐ Rep</button>
                            <button class="btn ${u.followedByCurrentUser ? 'btn-danger' : 'btn-primary'} btn-sm" onclick="toggleFollow(${u.id})" id="followBtn">${u.followedByCurrentUser ? '❌ Smetti di seguire' : '➕ Segui'}</button>` : ''}
                    </div>
                </div>
            </div>`;

        if (u.bio) html += `<div class="profile-bio">${escapeHtml(u.bio)}</div>`;

        // Advanced profile info
        const infoItems = [];
        if (u.location) infoItems.push(`📍 ${escapeHtml(u.location)}`);
        if (u.website) infoItems.push(`🌐 <a href="${escapeHtml(u.website)}" target="_blank" rel="noopener">${escapeHtml(u.website)}</a>`);
        if (u.jabber) infoItems.push(`💬 ${escapeHtml(u.jabber)}`);
        if (u.birthday) infoItems.push(`🎂 ${new Date(u.birthday).toLocaleDateString('it-IT')}`);
        if (infoItems.length) html += `<div class="profile-info-extra">${infoItems.join(' · ')}</div>`;
        
        // Edit form (hidden by default) 
        if (isOwnProfile) {
            html += `<div class="profile-edit-form" id="profileEditForm" style="display:none">
                <label>Bio</label>
                <textarea id="editBio" rows="3">${escapeHtml(u.bio || '')}</textarea>
                <label>Firma</label>
                <input type="text" id="editSignature" value="${escapeHtml(u.signature || '')}" maxlength="200">
                <label>Banner URL</label>
                <input type="text" id="editBannerUrl" value="${escapeHtml(u.bannerUrl || '')}" placeholder="https://..." maxlength="500">
                <label>Sito Web</label>
                <input type="text" id="editWebsite" value="${escapeHtml(u.website || '')}" placeholder="https://..." maxlength="200">
                <label>Posizione</label>
                <input type="text" id="editLocation" value="${escapeHtml(u.location || '')}" maxlength="100">
                <label>Jabber/XMPP</label>
                <input type="text" id="editJabber" value="${escapeHtml(u.jabber || '')}" maxlength="100">
                <label>Data di nascita</label>
                <input type="date" id="editBirthday" value="${u.birthday ? new Date(u.birthday).toISOString().split('T')[0] : ''}">
                <div style="margin-top:0.6rem;display:flex;gap:0.5rem">
                    <button class="btn btn-primary btn-sm" onclick="saveProfile(${u.id})">Salva</button>
                    <button class="btn btn-outline btn-sm" onclick="document.getElementById('profileEditForm').style.display='none'">Annulla</button>
                </div>
            </div>`;
        }

        // Tabs
        html += `<div class="profile-tabs">
            <button class="active" onclick="showProfileTab('reputation',this)">⭐ Reputazione (${repHistory.length})</button>
            <button onclick="showProfileTab('followers',this);loadFollowersTab(${u.id})">👥 Follower (${u.followerCount})</button>
            <button onclick="showProfileTab('following',this);loadFollowingTab(${u.id})">👤 Seguiti (${u.followingCount})</button>
        </div>`;

        // Reputation tab
        html += `<div id="profileTabReputation" class="rep-list">`;
        if (repHistory.length === 0) {
            html += '<div class="empty-state"><p>Nessuna reputazione ricevuta</p></div>';
        } else {
            html += repHistory.map(r => `<div class="rep-item ${r.points > 0 ? 'positive' : 'negative'}">
                <span class="rep-points" style="color:${r.points > 0 ? 'var(--accent)' : 'var(--danger)'}">${r.points > 0 ? '+' : ''}${r.points}</span>
                <span class="rep-from" onclick="navigate('profile',{id:${r.giverId}})" style="cursor:pointer">${escapeHtml(r.giverName)}</span>
                <span class="rep-comment">${r.comment ? escapeHtml(r.comment) : ''}</span>
                <span class="rep-date">${timeAgo(r.createdAt)}</span>
            </div>`).join('');
        }
        html += '</div>';

        // Followers tab
        html += '<div id="profileTabFollowers" class="follow-list" style="display:none"><div class="loading">Caricamento...</div></div>';
        // Following tab
        html += '<div id="profileTabFollowing" class="follow-list" style="display:none"><div class="loading">Caricamento...</div></div>';

        container.innerHTML = html;
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Utente non trovato</p></div>';
    }
}

function showProfileTab(tab, btn) {
    document.querySelectorAll('.profile-tabs button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('profileTabReputation').style.display = tab === 'reputation' ? '' : 'none';
    const ft = document.getElementById('profileTabFollowers');
    const fgt = document.getElementById('profileTabFollowing');
    if (ft) ft.style.display = tab === 'followers' ? '' : 'none';
    if (fgt) fgt.style.display = tab === 'following' ? '' : 'none';
}

function toggleProfileEdit(userId) {
    const form = document.getElementById('profileEditForm');
    form.style.display = form.style.display === 'none' ? '' : 'none';
}

async function saveProfile(userId) {
    try {
        const birthdayVal = document.getElementById('editBirthday')?.value;
        await api(`/users/${userId}/profile`, {
            method: 'PUT',
            body: JSON.stringify({
                bio: document.getElementById('editBio').value.trim() || null,
                signature: document.getElementById('editSignature').value.trim() || null,
                bannerUrl: document.getElementById('editBannerUrl')?.value.trim() || null,
                website: document.getElementById('editWebsite')?.value.trim() || null,
                location: document.getElementById('editLocation')?.value.trim() || null,
                jabber: document.getElementById('editJabber')?.value.trim() || null,
                birthday: birthdayVal ? new Date(birthdayVal).toISOString() : null
            })
        });
        showToast('Profilo aggiornato', 'success');
        loadProfile(userId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Reputation ===
function setRepPoints(val) {
    document.getElementById('repPoints').value = val;
    document.querySelectorAll('.rep-btn').forEach(b => b.classList.remove('active'));
    if (val === 1) document.querySelector('.rep-pos').classList.add('active');
    else document.querySelector('.rep-neg').classList.add('active');
}

async function submitReputation(e) {
    e.preventDefault();
    const errEl = document.getElementById('repError');
    errEl.textContent = '';
    try {
        await api('/reputation', {
            method: 'POST',
            body: JSON.stringify({
                userId: parseInt(document.getElementById('repUserId').value),
                points: parseInt(document.getElementById('repPoints').value),
                comment: document.getElementById('repComment').value.trim() || null
            })
        });
        closeModal();
        document.getElementById('repComment').value = '';
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore';
    }
}

// === Marketplace ===
let marketType = null;
let marketCategory = null;
let marketSearch = '';
let marketPage = 1;
let marketSort = '';
let marketCategoriesCache = null;

async function checkVendorStatus() {
    if (!currentUser) return false;
    try {
        const res = await api('/vendor/status');
        currentUser.isVendor = res.isVendor;
        return res.isVendor;
    } catch { return false; }
}

function setMarketType(type) {
    marketType = type;
    marketPage = 1;
    document.querySelectorAll('.market-type-pills .filter-btn').forEach(b => b.classList.remove('active'));
    const id = type ? `filter${type.charAt(0).toUpperCase() + type.slice(1)}` : 'filterAll';
    const el = document.getElementById(id);
    if (el) el.classList.add('active');
    loadMarketplace();
}

function setMarketCategory(catId) {
    marketCategory = catId;
    marketPage = 1;
    document.querySelectorAll('.market-cat-item').forEach(el => el.classList.remove('active'));
    const active = document.querySelector(`.market-cat-item[data-cat="${catId || 'all'}"]`);
    if (active) active.classList.add('active');
    loadMarketplace();
}

async function loadMarketCategories() {
    const container = document.getElementById('marketCatList');
    if (!container) return;
    try {
        const cats = marketCategoriesCache || await api('/categories');
        marketCategoriesCache = cats;
        let html = `<div class="market-cat-item active" data-cat="all" onclick="setMarketCategory(null)">📋 Tutte le categorie</div>`;
        for (const cat of cats) {
            if (!cat.isMarketplace) continue;
            html += `<div class="market-cat-item parent" data-cat="${cat.id}" onclick="setMarketCategory(${cat.id})">${cat.icon} ${escapeHtml(cat.name)}</div>`;
            if (cat.subCategories?.length) {
                for (const sub of cat.subCategories) {
                    html += `<div class="market-cat-item child" data-cat="${sub.id}" onclick="setMarketCategory(${sub.id})">${sub.icon} ${escapeHtml(sub.name)}</div>`;
                }
            }
        }
        container.innerHTML = html;
    } catch {
        container.innerHTML = '<div class="empty-state">Errore categorie</div>';
    }
}

async function loadMarketplace() {
    const grid = document.getElementById('marketplaceGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        // Load categories sidebar (only first time)
        if (!marketCategoriesCache) loadMarketCategories();

        // Load stats
        const stats = await api('/marketplace/stats');
        const statsEl = document.getElementById('marketStats');
        if (statsEl) statsEl.innerHTML = `
            <div class="market-stat"><span class="market-stat-num">${stats.totalListings}</span><span>Annunci</span></div>
            <div class="market-stat"><span class="market-stat-num">${stats.totalVendors}</span><span>Venditori</span></div>
            <div class="market-stat"><span class="market-stat-num">${stats.totalSold}</span><span>Venduti</span></div>
            <div class="market-stat"><span class="market-stat-num">${stats.totalOrders}</span><span>Ordini</span></div>`;

        // Filters
        const params = new URLSearchParams();
        if (marketType) params.set('type', marketType);
        if (marketCategory) params.set('categoryId', marketCategory);
        if (marketSearch) params.set('search', marketSearch);
        if (marketSort) params.set('sort', marketSort);
        params.set('page', marketPage);

        const data = await api(`/marketplace?${params}`);
        const listings = data.listings || data;
        if (!listings || listings.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">🛒</div><p>Nessun annuncio trovato</p></div>';
            return;
        }

        grid.innerHTML = listings.map(l => {
            const typeClass = (l.type || '').toLowerCase();
            const typeIcon = l.type === 'Digital' ? '🖥️' : l.type === 'Physical' ? '📦' : '⚙️';
            const deliveryText = l.deliveryType === 'Instant' ? '⚡ Istantaneo' : l.deliveryType === 'Manual' ? '🤝 Manuale' : '🚚 Spedizione';
            const stockHtml = l.stock > 0
                ? `<span class="marketplace-card-stock">📦 ${l.stock} disp.</span>`
                : `<span class="marketplace-card-stock out">❌ Esaurito</span>`;
            return `<div class="marketplace-card" onclick="navigate('listingDetail',{id:${l.id}})">
                ${l.imageUrl ? `<div class="marketplace-card-img"><img src="${escapeHtml(l.imageUrl)}" alt="" loading="lazy"><span class="marketplace-card-badge ${typeClass}">${typeIcon} ${escapeHtml(l.type)}</span></div>` : ''}
                <div class="marketplace-card-body">
                    <div class="marketplace-card-header">
                        <span class="marketplace-card-title">${escapeHtml(l.title)}</span>
                        <span class="marketplace-card-price">${l.priceCrypto} ${escapeHtml(l.currency)}</span>
                    </div>
                    <div class="marketplace-card-tags">
                        ${!l.imageUrl ? `<span class="marketplace-card-type ${typeClass}">${typeIcon} ${escapeHtml(l.type)}</span>` : ''}
                        <span class="marketplace-card-delivery">${deliveryText}</span>
                        ${l.categoryName ? `<span class="marketplace-card-category">${escapeHtml(l.categoryName)}</span>` : ''}
                    </div>
                    <div class="marketplace-card-desc">${escapeHtml(l.description).substring(0, 120)}${l.description.length > 120 ? '...' : ''}</div>
                </div>
                <div class="marketplace-card-footer">
                    <span class="marketplace-card-seller" onclick="event.stopPropagation();navigate('vendorProfile',{id:${l.sellerId}})">
                        ${l.isVendor ? '✅' : '👤'} ${escapeHtml(l.sellerName)}
                    </span>
                    ${stockHtml}
                </div>
            </div>`;
        }).join('');

        // Pagination
        const pag = document.getElementById('marketplacePagination');
        const totalPages = Math.ceil((data.total || listings.length) / 20);
        if (totalPages > 1) {
            let html = '';
            for (let i = 1; i <= totalPages; i++) {
                html += `<button class="page-btn ${i === marketPage ? 'active' : ''}" onclick="marketPage=${i};loadMarketplace()">${i}</button>`;
            }
            pag.innerHTML = html;
        } else {
            pag.innerHTML = '';
        }
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore caricamento marketplace</p></div>';
    }
}

async function loadListingDetail(id) {
    const container = document.getElementById('listingDetail');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const [l, reviewData] = await Promise.all([
            api(`/marketplace/${id}`),
            api(`/reviews/listing/${id}`).catch(() => ({ reviews: [], averageRating: 0 }))
        ]);

        const starsHtml = (rating) => {
            let s = '';
            for (let i = 1; i <= 5; i++) s += i <= Math.round(rating) ? '⭐' : '☆';
            return s;
        };

        let reviewsSection = '';
        if (reviewData.reviews && reviewData.reviews.length > 0) {
            reviewsSection = `
                <div class="listing-reviews-section" style="margin-top:1.5rem">
                    <h3>📝 Recensioni (${reviewData.reviews.length}) — Media: ${starsHtml(reviewData.averageRating)} ${reviewData.averageRating.toFixed(1)}/5</h3>
                    <div class="reviews-list">
                        ${reviewData.reviews.map(r => `
                            <div class="review-card" style="background:var(--bg-card);border:1px solid var(--border);border-radius:var(--radius);padding:0.8rem;margin-bottom:0.5rem">
                                <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.4rem">
                                    <span class="review-author" style="cursor:pointer;color:var(--accent);font-weight:600" onclick="navigate('profile',{id:${r.reviewerId}})">${escapeHtml(r.reviewerName)}</span>
                                    <span style="font-size:12px;color:var(--text-muted)">${timeAgo(r.createdAt)}</span>
                                </div>
                                <div class="review-stars">${starsHtml(r.rating)}</div>
                                ${r.comment ? `<p style="margin:0.4rem 0 0;font-size:13px;color:var(--text-secondary)">${escapeHtml(r.comment)}</p>` : ''}
                            </div>`).join('')}
                    </div>
                </div>`;
        } else {
            reviewsSection = `<div class="listing-reviews-section" style="margin-top:1.5rem">
                <h3>📝 Recensioni</h3>
                <p style="color:var(--text-muted);font-size:13px">Nessuna recensione ancora per questo prodotto</p>
            </div>`;
        }

        container.innerHTML = `
            <button class="btn btn-outline btn-sm" onclick="navigate('marketplace')" style="margin-bottom:1rem">← Torna al Market</button>
            <div class="listing-detail-card">
                ${l.imageUrl ? `<div class="listing-detail-img"><img src="${escapeHtml(l.imageUrl)}" alt=""></div>` : ''}
                <div class="listing-detail-body">
                    <h2>${escapeHtml(l.title)}</h2>
                    <div class="listing-detail-meta">
                        <span class="listing-price-big">${l.priceCrypto} ${escapeHtml(l.currency)}</span>
                        <span class="marketplace-card-type ${l.type.toLowerCase()}">${l.type === 'Digital' ? '🖥️' : l.type === 'Physical' ? '📦' : '⚙️'} ${escapeHtml(l.type)}</span>
                        <span class="marketplace-card-delivery">${l.deliveryType === 'Instant' ? '⚡ Istantaneo' : l.deliveryType === 'Manual' ? '🤝 Manuale' : '🚚 Spedizione'}</span>
                    </div>
                    <div class="listing-detail-desc">${formatContent(l.description)}</div>
                    ${l.shippingInfo ? `<div class="listing-ship-info">🚚 <strong>Spedizione:</strong> ${escapeHtml(l.shippingInfo)}</div>` : ''}
                    <div class="listing-detail-stats">
                        <span>📦 Stock: ${l.stock}</span>
                        <span>🛒 Venduti: ${l.soldCount}</span>
                        <span>📅 ${timeAgo(l.createdAt)}</span>
                    </div>
                    <div class="listing-seller-box" onclick="navigate('vendorProfile',{id:${l.sellerId}})">
                        <div class="listing-seller-avatar">${l.sellerAvatarUrl ? `<img src="${escapeHtml(l.sellerAvatarUrl)}" alt="">` : l.sellerName.charAt(0).toUpperCase()}</div>
                        <div class="listing-seller-info">
                            <strong>${l.isVendor ? '✅' : ''} ${escapeHtml(l.sellerName)}</strong>
                            <span>⭐ ${l.sellerReputation} rep · ${l.sellerSalesCount} vendite</span>
                            ${l.vendorBio ? `<span class="listing-seller-bio">${escapeHtml(l.vendorBio)}</span>` : ''}
                        </div>
                    </div>
                    <div class="listing-detail-actions">
                        ${currentUser && currentUser.userId !== l.sellerId && l.stock > 0 && l.status === 'Active' ? `
                            <button class="btn btn-primary btn-lg" onclick="showOrderModal(${l.id}, '${escapeHtml(l.title)}', ${l.priceCrypto}, '${escapeHtml(l.currency)}', '${l.deliveryType}', ${l.stock})">🛒 Acquista con Escrow</button>
                        ` : ''}
                        ${currentUser ? `<button class="btn btn-outline" onclick="toggleWishlist(${l.id})">❤️ Wishlist</button>` : ''}
                        ${currentUser && currentUser.userId !== l.sellerId ? `
                            <button class="btn btn-accent" onclick="openChatWith(${l.sellerId},'${escapeHtml(l.sellerName).replace(/'/g, "\\'")}')">💬 Chat Venditore</button>
                            <button class="btn btn-outline" onclick="navigate('messages',{userId:${l.sellerId}})">✉️ Messaggio</button>
                        ` : ''}
                        ${l.stock <= 0 ? '<span class="sold-out-badge">❌ ESAURITO</span>' : ''}
                    </div>
                    ${reviewsSection}
                </div>
            </div>`;
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Annuncio non trovato</p></div>';
    }
}

function showOrderModal(listingId, title, price, currency, deliveryType, maxStock) {
    showModal('orderCreate');
    const content = document.getElementById('orderCreateContent');
    content.innerHTML = `
        <div class="order-summary">
            <h3>${escapeHtml(title)}</h3>
            <p class="order-price">${price} ${escapeHtml(currency)}</p>
            <form onsubmit="createOrder(event, ${listingId})">
                <div class="form-row">
                    <label>Quantità</label>
                    <input type="number" id="orderQuantity" min="1" max="${maxStock}" value="1" onchange="updateOrderTotal(${price}, '${escapeHtml(currency)}')">
                </div>
                ${deliveryType === 'Shipping' ? `
                    <div class="form-group">
                        <label>Indirizzo di spedizione</label>
                        <textarea id="orderShippingAddress" placeholder="Inserisci l'indirizzo completo..." rows="3" required></textarea>
                    </div>` : ''}
                <div class="order-total">
                    <span>Totale:</span>
                    <strong id="orderTotalPrice">${price} ${escapeHtml(currency)}</strong>
                </div>
                <div class="escrow-info">
                    <p>🔒 <strong>Pagamento tramite Escrow</strong></p>
                    <p>I fondi verranno trattenuti in escrow fino alla consegna e conferma.</p>
                    ${deliveryType === 'Instant' ? '<p>⚡ Il contenuto verrà consegnato automaticamente dopo il pagamento.</p>' : ''}
                </div>
                <div class="modal-error" id="orderError"></div>
                <button type="submit" class="btn btn-primary btn-full">Procedi all'acquisto</button>
            </form>
        </div>`;
}

function updateOrderTotal(price, currency) {
    const qty = parseInt(document.getElementById('orderQuantity').value) || 1;
    document.getElementById('orderTotalPrice').textContent = `${(price * qty).toFixed(4)} ${currency}`;
}

async function createOrder(e, listingId) {
    e.preventDefault();
    const errEl = document.getElementById('orderError');
    errEl.textContent = '';
    try {
        const qty = parseInt(document.getElementById('orderQuantity').value) || 1;
        const shippingEl = document.getElementById('orderShippingAddress');
        const res = await api('/orders', {
            method: 'POST',
            body: JSON.stringify({
                listingId,
                quantity: qty,
                shippingAddress: shippingEl ? shippingEl.value.trim() : null
            })
        });
        closeModal();
        navigate('orderDetail', { id: res.orderId });
        showToast('Ordine creato! Procedi con il pagamento escrow.', 'success');
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore creazione ordine';
    }
}

async function createListing(e) {
    e.preventDefault();
    const errEl = document.getElementById('listingError');
    errEl.textContent = '';
    try {
        const deliveryType = document.getElementById('listingDelivery').value;
        const body = {
            title: document.getElementById('listingTitle').value.trim(),
            description: document.getElementById('listingDesc').value.trim(),
            priceCrypto: parseFloat(document.getElementById('listingPrice').value),
            currency: document.getElementById('listingCurrency').value,
            type: document.getElementById('listingType').value,
            deliveryType: deliveryType,
            categoryId: parseInt(document.getElementById('listingCategory').value),
            stock: parseInt(document.getElementById('listingStock').value) || 1,
            imageUrl: document.getElementById('listingImage').value.trim() || null,
            digitalContent: deliveryType === 'Instant' ? document.getElementById('listingDigitalContent').value.trim() : null,
            shippingInfo: deliveryType === 'Shipping' ? document.getElementById('listingShippingInfo').value.trim() : null
        };
        const res = await api('/marketplace', { method: 'POST', body: JSON.stringify(body) });
        closeModal();
        if (res.status === 'PendingApproval') {
            showToast('Annuncio inviato! In attesa di approvazione dallo staff.', 'info');
        } else {
            showToast('Annuncio pubblicato!', 'success');
        }
        loadMarketplace();
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore';
    }
}

// Toggle conditional fields in listing form
document.addEventListener('change', (e) => {
    if (e.target.id === 'listingDelivery') {
        const dc = document.getElementById('listingDigitalContent');
        const si = document.getElementById('listingShippingInfo');
        dc.style.display = e.target.value === 'Instant' ? '' : 'none';
        si.style.display = e.target.value === 'Shipping' ? '' : 'none';
    }
});

// === Vendors ===
async function loadVendors() {
    const grid = document.getElementById('vendorsGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const vendors = await api('/vendors');
        if (!vendors || vendors.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">🏪</div><p>Nessun venditore ancora</p></div>';
            return;
        }
        grid.innerHTML = vendors.map(v => `<div class="vendor-card" onclick="navigate('vendorProfile',{id:${v.id}})">
            <div class="vendor-card-avatar">${v.avatarUrl ? `<img src="${escapeHtml(v.avatarUrl)}" alt="">` : v.username.charAt(0).toUpperCase()}</div>
            <div class="vendor-card-info">
                <strong>✅ ${escapeHtml(v.username)}</strong>
                <span>⭐ ${v.reputationScore} rep · ${v.totalSales} vendite · ${v.activeListings} annunci</span>
                ${v.vendorBio ? `<span class="vendor-card-bio">${escapeHtml(v.vendorBio)}</span>` : ''}
            </div>
        </div>`).join('');
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function loadVendorProfile(id) {
    const container = document.getElementById('vendorProfile');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const data = await api(`/marketplace/vendor/${id}`);
        const v = data.vendor;
        container.innerHTML = `
            <button class="btn btn-outline btn-sm" onclick="navigate('vendors')" style="margin-bottom:1rem">← Venditori</button>
            <div class="vendor-profile-header">
                <div class="vendor-profile-avatar">${v.avatarUrl ? `<img src="${escapeHtml(v.avatarUrl)}" alt="">` : v.username.charAt(0).toUpperCase()}</div>
                <div class="vendor-profile-info">
                    <h2>✅ ${escapeHtml(v.username)}</h2>
                    ${v.vendorBio ? `<p>${escapeHtml(v.vendorBio)}</p>` : ''}
                    <div class="vendor-profile-stats">
                        <span>⭐ ${v.reputationScore} Reputazione</span>
                        <span>🛒 ${v.totalSales} Vendite</span>
                        <span>📦 ${v.activeListings} Annunci attivi</span>
                        ${v.vendorSince ? `<span>📅 Venditore dal ${new Date(v.vendorSince).toLocaleDateString('it-IT')}</span>` : ''}
                    </div>
                    <button class="btn btn-outline btn-sm" onclick="navigate('messages',{userId:${v.id}})">✉️ Contatta</button>
                </div>
            </div>
            <h3 style="margin-top:1.5rem">Annunci di ${escapeHtml(v.username)}</h3>
            <div class="marketplace-grid">${data.listings.length === 0 ? '<p>Nessun annuncio attivo</p>' :
                data.listings.map(l => {
                    const typeClass = (l.type || '').toLowerCase();
                    const typeIcon = l.type === 'Digital' ? '🖥️' : l.type === 'Physical' ? '📦' : '⚙️';
                    return `<div class="marketplace-card" onclick="navigate('listingDetail',{id:${l.id}})">
                    ${l.imageUrl ? `<div class="marketplace-card-img"><img src="${escapeHtml(l.imageUrl)}" alt="" loading="lazy"><span class="marketplace-card-badge ${typeClass}">${typeIcon} ${escapeHtml(l.type)}</span></div>` : ''}
                    <div class="marketplace-card-body">
                        <div class="marketplace-card-header">
                            <span class="marketplace-card-title">${escapeHtml(l.title)}</span>
                            <span class="marketplace-card-price">${l.priceCrypto} ${escapeHtml(l.currency)}</span>
                        </div>
                        <div class="marketplace-card-desc">${escapeHtml(l.description).substring(0, 100)}</div>
                    </div>
                </div>`;
                }).join('')}}
            </div>`;
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

// === Vendor Application ===
async function submitVendorApplication(e) {
    e.preventDefault();
    const errEl = document.getElementById('vendorError');
    errEl.textContent = '';
    try {
        await api('/vendor/apply', {
            method: 'POST',
            body: JSON.stringify({
                telegramUsername: document.getElementById('vendorTelegram').value.trim(),
                motivation: document.getElementById('vendorMotivation').value.trim(),
                specialization: document.getElementById('vendorSpecialization').value.trim() || null
            })
        });
        closeModal();
        showToast('Richiesta venditore inviata! Verrai contattato dallo staff.', 'success');
    } catch (err) {
        errEl.textContent = err.data?.error || 'Errore';
    }
}

// === My Listings ===
async function loadMyListings() {
    const grid = document.getElementById('myListingsGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const listings = await api('/marketplace/my');
        if (!listings || listings.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">📦</div><p>Nessun annuncio pubblicato</p></div>';
            return;
        }
        grid.innerHTML = listings.map(l => `<div class="marketplace-card my-listing-card">
            <div class="my-listing-status status-${l.status.toLowerCase()}">${getStatusLabel(l.status)}</div>
            ${l.imageUrl ? `<div class="marketplace-card-img"><img src="${escapeHtml(l.imageUrl)}" alt=""></div>` : ''}
            <div class="marketplace-card-header">
                <span class="marketplace-card-title">${escapeHtml(l.title)}</span>
                <span class="marketplace-card-price">${l.priceCrypto} ${escapeHtml(l.currency)}</span>
            </div>
            <div class="marketplace-card-tags">
                <span class="marketplace-card-type ${l.type.toLowerCase()}">${escapeHtml(l.type)}</span>
                <span>📦 ${l.stock} · 🛒 ${l.soldCount} venduti</span>
            </div>
            ${l.rejectionReason ? `<div class="rejection-reason">❌ Motivo rifiuto: ${escapeHtml(l.rejectionReason)}</div>` : ''}
            <div class="my-listing-actions">
                <button class="btn btn-sm btn-outline" onclick="navigate('listingDetail',{id:${l.id}})">👁️ Vedi</button>
                ${l.status === 'Active' ? `<button class="btn btn-sm btn-danger" onclick="updateListingStatus(${l.id},'Closed')">Chiudi</button>` : ''}
                ${l.status === 'Closed' || l.status === 'Rejected' ? `<button class="btn btn-sm btn-primary" onclick="updateListingStatus(${l.id},'Active')">Riattiva</button>` : ''}
            </div>
        </div>`).join('');
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

function getStatusLabel(status) {
    const labels = {
        'PendingApproval': '⏳ In attesa',
        'Active': '✅ Attivo',
        'Sold': '🛒 Venduto',
        'Closed': '🔒 Chiuso',
        'Rejected': '❌ Rifiutato'
    };
    return labels[status] || status;
}

async function updateListingStatus(id, status) {
    try {
        await api(`/marketplace/${id}/status?status=${status}`, { method: 'PUT' });
        loadMyListings();
        showToast('Stato aggiornato', 'success');
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Orders ===
function setOrderTab(btn) {
    document.querySelectorAll('.orders-tabs button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

async function loadMyOrders(type = 'buying') {
    const list = document.getElementById('ordersList');
    list.innerHTML = '<div class="loading">Caricamento</div>';
    // Add export button if not present
    const ordersPage = document.getElementById('pageMyOrders');
    if (!ordersPage.querySelector('.export-csv-btn')) {
        const h2 = ordersPage.querySelector('h2');
        if (h2) {
            const wrapper = document.createElement('div');
            wrapper.className = 'page-header-row';
            wrapper.innerHTML = `<h2>📋 I Miei Ordini</h2><button class="btn btn-outline btn-sm export-csv-btn" onclick="exportOrdersCsv()">📥 Export CSV</button>`;
            h2.replaceWith(wrapper);
        }
    }
    try {
        const orders = await api(`/orders/${type}`);
        if (!orders || orders.length === 0) {
            list.innerHTML = '<div class="empty-state"><div class="empty-icon">📋</div><p>Nessun ordine</p></div>';
            return;
        }
        list.innerHTML = orders.map(o => `<div class="order-card" onclick="navigate('orderDetail',{id:${o.id}})">
            <div class="order-card-left">
                ${o.listingImageUrl ? `<img src="${escapeHtml(o.listingImageUrl)}" alt="" class="order-thumb">` : '<div class="order-thumb-placeholder">🛒</div>'}
                <div class="order-card-info">
                    <strong>${escapeHtml(o.listingTitle)}</strong>
                    <span>${o.amount} ${escapeHtml(o.currency)} · x${o.quantity}</span>
                    <span>${type === 'buying' ? `Venditore: ${escapeHtml(o.sellerName)}` : `Acquirente: ${escapeHtml(o.buyerName)}`}</span>
                </div>
            </div>
            <div class="order-card-right">
                <span class="order-status-badge status-${o.status.toLowerCase()}">${getOrderStatusLabel(o.status)}</span>
                <span class="order-escrow-badge escrow-${o.escrowStatus.toLowerCase()}">${getEscrowLabel(o.escrowStatus)}</span>
                <span class="order-date">${timeAgo(o.createdAt)}</span>
            </div>
        </div>`).join('');
    } catch {
        list.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

function getOrderStatusLabel(s) {
    const labels = { Created:'📝 Creato', EscrowFunded:'💰 Escrow', Processing:'⚙️ In lavorazione', Shipped:'🚚 Spedito', Delivered:'✅ Consegnato', Completed:'🏁 Completato', Disputed:'⚠️ Disputa', Cancelled:'❌ Annullato', Refunded:'💸 Rimborsato' };
    return labels[s] || s;
}

function getEscrowLabel(s) {
    const labels = { Pending:'⏳ In attesa', Funded:'💰 Finanziato', Released:'✅ Rilasciato', Refunded:'💸 Rimborsato', Disputed:'⚠️ Disputa' };
    return labels[s] || s;
}

async function loadOrderDetail(id) {
    const container = document.getElementById('orderDetail');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const o = await api(`/orders/${id}`);
        const isBuyer = currentUser && currentUser.userId === o.buyerId;
        const isSeller = currentUser && currentUser.userId === o.sellerId;

        container.innerHTML = `
            <button class="btn btn-outline btn-sm" onclick="navigate('myOrders')" style="margin-bottom:1rem">← I Miei Ordini</button>
            <div class="order-detail-card">
                <div class="order-detail-header">
                    <h2>Ordine #${o.id}</h2>
                    <span class="order-status-badge status-${o.status.toLowerCase()}">${getOrderStatusLabel(o.status)}</span>
                </div>
                <div class="order-detail-grid">
                    <div class="order-info-section">
                        <h3>📦 Dettagli</h3>
                        <p><strong>Prodotto:</strong> ${escapeHtml(o.listingTitle)}</p>
                        <p><strong>Quantità:</strong> x${o.quantity}</p>
                        <p><strong>Importo:</strong> ${o.amount} ${escapeHtml(o.currency)}</p>
                        <p><strong>Consegna:</strong> ${o.deliveryType === 'Instant' ? '⚡ Istantaneo' : o.deliveryType === 'Manual' ? '🤝 Manuale' : '🚚 Spedizione'}</p>
                        <p><strong>Data:</strong> ${new Date(o.createdAt).toLocaleString('it-IT')}</p>
                    </div>
                    <div class="order-info-section">
                        <h3>🔒 Escrow</h3>
                        <p><strong>Stato:</strong> <span class="escrow-${o.escrowStatus.toLowerCase()}">${getEscrowLabel(o.escrowStatus)}</span></p>
                        ${o.escrowWalletAddress && o.escrowStatus === 'Pending' ? `
                            <div class="escrow-pay-box">
                                <p>Invia <strong>${o.amount} ${escapeHtml(o.currency)}</strong> a:</p>
                                <code class="escrow-address">${escapeHtml(o.escrowWalletAddress)}</code>
                                <p class="escrow-note">Dopo il pagamento, inserisci il TX ID per confermare</p>
                            </div>` : ''}
                    </div>
                </div>
                ${o.shippingAddress ? `<div class="order-info-section"><h3>🚚 Spedizione</h3><p>${escapeHtml(o.shippingAddress)}</p>${o.trackingNumber ? `<p><strong>Tracking:</strong> ${escapeHtml(o.trackingNumber)}</p>` : ''}</div>` : ''}
                ${o.digitalDeliveryContent && isBuyer ? `<div class="order-info-section digital-content-box"><h3>📥 Contenuto Consegnato</h3><pre>${escapeHtml(o.digitalDeliveryContent)}</pre></div>` : ''}
                ${o.disputeReason ? `<div class="order-info-section dispute-box"><h3>⚠️ Disputa</h3><p>${escapeHtml(o.disputeReason)}</p></div>` : ''}
                <div class="order-detail-actions">
                    ${isBuyer && o.escrowStatus === 'Pending' ? `
                        <div class="fund-escrow-form">
                            <input type="text" id="fundTxId" placeholder="Inserisci TX ID del pagamento">
                            <button class="btn btn-primary" onclick="fundEscrow(${o.id})">💰 Conferma Pagamento</button>
                        </div>` : ''}
                    ${isBuyer && (o.status === 'Delivered' || o.status === 'Shipped') ? `<button class="btn btn-primary btn-lg" onclick="confirmOrder(${o.id})">✅ Conferma Ricezione (Rilascia Escrow)</button>` : ''}
                    ${isBuyer && o.status === 'Completed' ? `
                        <div class="review-form-box" id="reviewFormBox">
                            <h3>⭐ Lascia una Recensione</h3>
                            <div class="form-group">
                                <label>Rating</label>
                                <select id="reviewRating"><option value="5">⭐⭐⭐⭐⭐ (5)</option><option value="4">⭐⭐⭐⭐ (4)</option><option value="3">⭐⭐⭐ (3)</option><option value="2">⭐⭐ (2)</option><option value="1">⭐ (1)</option></select>
                            </div>
                            <div class="form-group">
                                <label>Commento (opzionale)</label>
                                <textarea id="reviewComment" rows="2" placeholder="La tua esperienza..."></textarea>
                            </div>
                            <button class="btn btn-primary btn-sm" onclick="submitReview(${o.id})">📝 Invia Recensione</button>
                        </div>` : ''}
                    ${isBuyer && o.escrowStatus === 'Funded' && o.status !== 'Completed' ? `<button class="btn btn-danger" onclick="disputeOrder(${o.id})">⚠️ Apri Disputa</button>` : ''}
                    ${isSeller && o.escrowStatus === 'Funded' && o.deliveryType === 'Shipping' && o.status === 'EscrowFunded' ? `
                        <div class="ship-form">
                            <input type="text" id="shipTracking" placeholder="Numero di tracking">
                            <button class="btn btn-primary" onclick="shipOrder(${o.id})">🚚 Segna come Spedito</button>
                        </div>` : ''}
                    ${isSeller && o.escrowStatus === 'Funded' && o.deliveryType === 'Manual' && o.status === 'EscrowFunded' ? `
                        <div class="deliver-form">
                            <textarea id="deliverContent" placeholder="Contenuto da consegnare al compratore..." rows="3"></textarea>
                            <button class="btn btn-primary" onclick="deliverOrder(${o.id})">📤 Consegna Contenuto</button>
                        </div>` : ''}
                    <button class="btn btn-outline" onclick="navigate('messages',{userId:${isBuyer ? o.sellerId : o.buyerId}})">✉️ ${isBuyer ? 'Contatta Venditore' : 'Contatta Acquirente'}</button>
                </div>
            </div>`;
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Ordine non trovato</p></div>';
    }
}

async function fundEscrow(orderId) {
    const txId = document.getElementById('fundTxId').value.trim();
    if (!txId) return showToast('Inserisci il TX ID', 'error');
    try {
        await api(`/orders/${orderId}/fund`, { method: 'PUT', body: JSON.stringify({ buyerTxId: txId }) });
        showToast('Pagamento confermato!', 'success');
        loadOrderDetail(orderId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function confirmOrder(orderId) {
    if (!confirm('Confermi di aver ricevuto il prodotto? L\'escrow verrà rilasciato al venditore.')) return;
    try {
        await api(`/orders/${orderId}/confirm`, { method: 'PUT' });
        showToast('Ordine completato! Escrow rilasciato.', 'success');
        loadOrderDetail(orderId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function disputeOrder(orderId) {
    const reason = prompt('Motivo della disputa:');
    if (!reason) return;
    try {
        await api(`/orders/${orderId}/dispute`, { method: 'PUT', body: JSON.stringify({ reason }) });
        showToast('Disputa aperta. Un admin esaminerà il caso.', 'info');
        loadOrderDetail(orderId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function shipOrder(orderId) {
    const tracking = document.getElementById('shipTracking').value.trim();
    if (!tracking) return showToast('Inserisci il tracking', 'error');
    try {
        await api(`/orders/${orderId}/ship`, { method: 'PUT', body: JSON.stringify({ trackingNumber: tracking }) });
        showToast('Ordine segnato come spedito!', 'success');
        loadOrderDetail(orderId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function deliverOrder(orderId) {
    const content = document.getElementById('deliverContent').value.trim();
    if (!content) return showToast('Inserisci il contenuto da consegnare', 'error');
    try {
        await api(`/orders/${orderId}/deliver`, { method: 'PUT', body: JSON.stringify({ content }) });
        showToast('Contenuto consegnato!', 'success');
        loadOrderDetail(orderId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Leaderboard ===
async function loadLeaderboard(type) {
    const list = document.getElementById('leaderboardList');
    list.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const entries = await api(`/leaderboard/${type}`);
        if (!entries || entries.length === 0) {
            list.innerHTML = '<div class="empty-state"><p>Nessun dato</p></div>';
            return;
        }
        list.innerHTML = entries.map((e, i) => {
            const topClass = i === 0 ? 'top-1' : (i === 1 ? 'top-2' : (i === 2 ? 'top-3' : ''));
            const avatarHtml = e.avatarUrl ? `<img src="${escapeHtml(e.avatarUrl)}" alt="">` : e.username.charAt(0).toUpperCase();
            let value;
            if (type === 'rating') {
                const stars = '⭐'.repeat(Math.round(e.avgRating || 0));
                value = `${stars} ${(e.avgRating || 0).toFixed(1)} (${e.reviewCount} recensioni)`;
            } else if (type === 'sales') {
                value = `🛒 ${e.totalSales} vendite · 💰 ${(e.totalRevenue || 0).toFixed(4)}`;
            } else {
                value = `⭐ ${e.reputationScore} rep`;
            }

            return `<div class="leaderboard-item ${topClass}">
                <span class="lb-rank">#${i + 1}</span>
                <div class="lb-avatar">${avatarHtml}</div>
                <span class="lb-name" onclick="navigate('vendorProfile',{id:${e.id}})">${escapeHtml(e.username)}</span>
                <span class="lb-value">${value}</span>
            </div>`;
        }).join('');
    } catch {
        list.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

function setLbTab(btn) {
    document.querySelectorAll('.leaderboard-tabs button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

// === Online Users (Members) ===
async function loadOnlineUsers() {
    const grid = document.getElementById('onlineUsersGrid');
    grid.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const users = await api('/leaderboard/online');
        if (!users || users.length === 0) {
            grid.innerHTML = '<div class="empty-state"><div class="empty-icon">👥</div><p>Nessun utente online</p></div>';
            return;
        }
        grid.innerHTML = users.map(u => {
            const avatarHtml = u.avatarUrl ? `<img src="${escapeHtml(u.avatarUrl)}" alt="">` : u.username.charAt(0).toUpperCase();
            const statusIcon = {online:'🟢',away:'🟡',busy:'🔴',offline:'⚫'}[u.status] || '🟢';
            const chatBtn = currentUser && u.id !== currentUser.userId
                ? `<button class="btn btn-outline btn-xs chat-user-btn" onclick="event.stopPropagation();openChatWith(${u.id},'${escapeHtml(u.username).replace(/'/g, "\\'")}')">💬 Chat</button>`
                : '';
            return `<div class="online-user-card" onclick="navigate('profile',{id:${u.id}})">
                <div class="online-user-avatar">
                    ${avatarHtml}
                    <div class="online-indicator ${u.status || 'online'}"></div>
                </div>
                <div class="online-user-info">
                    <div class="online-user-name">${escapeHtml(u.username)} ${statusIcon}</div>
                </div>
                ${chatBtn}
            </div>`;
        }).join('');
    } catch {
        grid.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

// === Avatar ===
function loadAvatarPreview() {
    const preview = document.getElementById('avatarPreview');
    if (currentUser) {
        api('/auth/me').then(me => {
            if (me.avatarUrl) preview.innerHTML = `<img src="${escapeHtml(me.avatarUrl)}" alt="">`;
            else preview.innerHTML = `<span style="font-size:2rem;color:var(--accent)">${me.username.charAt(0).toUpperCase()}</span>`;
        }).catch(() => { preview.innerHTML = '📷'; });
    }
}

async function uploadAvatar(e) {
    e.preventDefault();
    const errEl = document.getElementById('avatarError');
    errEl.textContent = '';
    const fileInput = document.getElementById('avatarFile');
    if (!fileInput.files.length) { errEl.textContent = 'Seleziona un file'; return; }
    const formData = new FormData();
    formData.append('avatar', fileInput.files[0]);
    try {
        await apiRaw('/avatar/upload', { method: 'POST', body: formData });
        closeModal();
        refreshMe();
        if (document.getElementById('pageProfile').style.display !== 'none') loadProfile(currentUser.userId);
    } catch (err) { errEl.textContent = err.data?.error || 'Errore nel caricamento'; }
}

async function removeAvatar() {
    try {
        await api('/avatar', { method: 'DELETE' });
        closeModal();
        refreshMe();
        if (document.getElementById('pageProfile').style.display !== 'none') loadProfile(currentUser.userId);
    } catch { /* ignore */ }
}

// === Notifications ===
async function loadDashNotifications() {
    const container = document.getElementById('dashNotificationsList');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const notifs = await api('/notifications');
        if (notifs.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">🔔</div><p>Nessuna notifica</p></div>';
            return;
        }
        container.innerHTML = notifs.map(n => {
            const iconMap = { Reply: '💬', Like: '❤️', Message: '✉️', Mention: '📣', System: '⚙️' };
            const icon = iconMap[n.type] || '🔔';
            const unread = n.isRead ? '' : ' unread';
            const clickAction = n.listingId ? `navigate('listingDetail',{id:${n.listingId}})` : '';
            return `<div class="notification-item${unread}" onclick="${clickAction};markNotifRead(${n.id})">
                <span class="notification-icon">${icon}</span>
                <div class="notification-body">
                    <div class="notification-text">${escapeHtml(n.message)}</div>
                    <div class="notification-time">${timeAgo(n.createdAt)}${n.fromUsername ? ` · ${escapeHtml(n.fromUsername)}` : ''}</div>
                </div>
            </div>`;
        }).join('');
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore nel caricamento delle notifiche</p></div>';
    }
}

async function markNotifRead(id) {
    try { await api(`/notifications/${id}/read`, { method: 'PUT' }); updateBadges(); } catch { /* ignore */ }
}
async function markAllNotificationsRead() {
    try { await api('/notifications/read-all', { method: 'PUT' }); updateBadges(); loadDashNotifications(); } catch { /* ignore */ }
}
async function deleteNotification(id) {
    try { await api(`/notifications/${id}`, { method: 'DELETE' }); loadDashNotifications(); updateBadges(); } catch { /* ignore */ }
}

// === Messages ===
async function loadConversations() {
    const container = document.getElementById('messagesContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const convos = await api('/messages/conversations');
        if (convos.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">✉️</div><p>Nessun messaggio</p></div>';
            return;
        }
        container.innerHTML = convos.map(c => {
            const avatarHtml = c.avatarUrl
                ? `<img src="${escapeHtml(c.avatarUrl)}" alt="">`
                : c.username.charAt(0).toUpperCase();
            const unread = c.unreadCount > 0 ? ' unread' : '';
            return `<div class="message-item${unread}" onclick="loadConversation(${c.userId})" style="cursor:pointer">
                <div class="message-avatar">${avatarHtml}</div>
                <div class="message-body">
                    <div class="message-from">${escapeHtml(c.username)}</div>
                    <div class="message-content">${escapeHtml(c.lastMessage)}</div>
                    <div class="message-time">${timeAgo(c.lastMessageAt)} ${c.unreadCount > 0 ? `· <strong>${c.unreadCount} non letti</strong>` : ''}</div>
                </div>
            </div>`;
        }).join('');
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function loadConversation(otherId) {
    const container = document.getElementById('messagesContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const messages = await api(`/messages/${otherId}`);
        const otherName = messages.length > 0
            ? (messages[0].senderId === otherId ? messages[0].senderName : messages[0].receiverName)
            : 'Utente';

        let html = `<span style="cursor:pointer;color:var(--accent);font-size:13px" onclick="loadConversations()">← Conversazioni</span>
            <h3 style="margin:0.5rem 0 1rem;font-family:'Orbitron',sans-serif;font-size:16px">${escapeHtml(otherName)}</h3>
            <div style="display:flex;flex-direction:column;gap:0.4rem;margin-bottom:1rem">`;

        html += messages.map(m => {
            const isSent = m.senderId === currentUser.userId;
            return `<div class="message-item" style="border-left:3px solid ${isSent ? 'var(--accent-cyan)' : 'var(--accent)'}">
                <div class="message-body">
                    <div class="message-from" style="color:${isSent ? 'var(--accent-cyan)' : 'var(--accent)'}">${isSent ? 'Tu' : escapeHtml(m.senderName)}</div>
                    <div class="message-content">${escapeHtml(m.content)}</div>
                    <div class="message-time">${timeAgo(m.createdAt)}</div>
                </div>
            </div>`;
        }).join('');

        html += `</div>
            <div class="reply-box">
                <textarea id="chatInput" placeholder="Scrivi un messaggio..." rows="3" onkeydown="if(event.key==='Enter'&&!event.shiftKey){event.preventDefault();sendChatMessage(${otherId})}"></textarea>
                <button class="btn btn-primary" onclick="sendChatMessage(${otherId})">Invia</button>
            </div>`;
        container.innerHTML = html;
        updateBadges();
    } catch {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function sendChatMessage(receiverId) {
    const input = document.getElementById('chatInput');
    const content = input.value.trim();
    if (!content) return;
    try {
        await api('/messages', { method: 'POST', body: JSON.stringify({ receiverId, content }) });
        loadConversation(receiverId);
    } catch (err) { alert(err.data?.error || 'Errore'); }
}

async function sendMessageFromModal(e) {
    e.preventDefault();
    const errEl = document.getElementById('msgError');
    errEl.textContent = '';
    const receiverId = parseInt(document.getElementById('msgReceiverId').value);
    const content = document.getElementById('msgContent').value.trim();
    if (!content) { errEl.textContent = 'Scrivi un messaggio'; return; }
    try {
        await api('/messages', { method: 'POST', body: JSON.stringify({ receiverId, content }) });
        closeModal();
        document.getElementById('msgContent').value = '';
    } catch (err) { errEl.textContent = err.data?.error || 'Errore'; }
}

// === Admin Panel ===
let adminTab = 'stats';

async function loadAdmin() {
    const panel = document.getElementById('adminPanel');
    panel.innerHTML = `
        <h2>⚙️ Pannello Admin</h2>
        <div class="profile-tabs" style="margin-bottom:1rem;flex-wrap:wrap">
            <button class="${adminTab === 'stats' ? 'active' : ''}" onclick="adminTab='stats';loadAdmin()">📊 Stats</button>
            <button class="${adminTab === 'analytics' ? 'active' : ''}" onclick="adminTab='analytics';loadAdmin()">📈 Analytics</button>
            <button class="${adminTab === 'users' ? 'active' : ''}" onclick="adminTab='users';loadAdmin()">👥 Utenti</button>
            <button class="${adminTab === 'listings' ? 'active' : ''}" onclick="adminTab='listings';loadAdmin()">🏪 Annunci</button>
            <button class="${adminTab === 'vendorApps' ? 'active' : ''}" onclick="adminTab='vendorApps';loadAdmin()">📋 Vendor</button>
            <button class="${adminTab === 'disputes' ? 'active' : ''}" onclick="adminTab='disputes';loadAdmin()">⚠️ Dispute</button>
            <button class="${adminTab === 'adminTickets' ? 'active' : ''}" onclick="adminTab='adminTickets';loadAdmin()">🎫 Ticket</button>
            <button class="${adminTab === 'logs' ? 'active' : ''}" onclick="adminTab='logs';loadAdmin()">📜 Logs</button>
        </div>
        <div id="adminContent"><div class="loading">Caricamento</div></div>`;

    const content = document.getElementById('adminContent');
    try {
        if (adminTab === 'stats') {
            const s = await api('/admin/stats');
            content.innerHTML = `
                <div class="admin-stats">
                    <div class="admin-stat"><div class="stat-value">👥 ${s.totalUsers}</div><div class="stat-label">Utenti</div></div>
                    <div class="admin-stat"><div class="stat-value">🏪 ${s.totalListings}</div><div class="stat-label">Annunci</div></div>
                    <div class="admin-stat"><div class="stat-value">🛒 ${s.totalOrders}</div><div class="stat-label">Ordini</div></div>
                    <div class="admin-stat"><div class="stat-value">💰 ${s.totalSales}</div><div class="stat-label">Vendite</div></div>
                    <div class="admin-stat"><div class="stat-value">🟢 ${s.onlineUsers}</div><div class="stat-label">Online</div></div>
                </div>
                <h3 style="margin:1.2rem 0 0.6rem;font-size:0.9rem;font-family:'Orbitron',sans-serif;letter-spacing:1px">⏰ Utenti recenti</h3>
                <div class="user-list">
                    ${s.recentUsers.map(u => `<div class="user-card" onclick="navigate('profile',{id:${u.id}})"><div class="user-card-body"><div class="user-card-title">${escapeHtml(u.username)}</div><div class="user-card-meta"><span>${timeAgo(u.createdAt)}</span></div></div></div>`).join('')}
                </div>`;
        } else if (adminTab === 'users') {
            await loadAdminUsers(content);
        } else if (adminTab === 'listings') {
            await loadAdminListings(content);
        } else if (adminTab === 'vendorApps') {
            await loadAdminVendorApps(content);
        } else if (adminTab === 'disputes') {
            await loadAdminDisputes(content);
        } else if (adminTab === 'analytics') {
            await loadAdminAnalytics(content);
        } else if (adminTab === 'logs') {
            await loadAdminLogs(content);
        } else if (adminTab === 'adminTickets') {
            await loadAdminTickets(content);
        }
    } catch (err) {
        content.innerHTML = err.status === 403
            ? '<div class="empty-state"><p>🚫 Accesso negato</p></div>'
            : '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>';
    }
}

async function loadAdminUsers(container, search = '') {
    const params = new URLSearchParams({ page: 1, pageSize: 50 });
    if (search) params.set('search', search);
    const result = await api(`/admin/users?${params}`);

    container.innerHTML = `
        <div class="admin-search">
            <input type="text" id="adminUserSearch" placeholder="Cerca utente..." value="${escapeHtml(search)}" onkeydown="if(event.key==='Enter')searchAdminUsers()">
            <button class="btn btn-outline btn-sm" onclick="searchAdminUsers()">🔍</button>
        </div>
        ${result.users.map(u => `
            <div class="admin-user-card ${u.isBanned ? 'banned' : ''}">
                <div class="post-author-avatar" style="width:32px;height:32px;font-size:14px">${u.username.charAt(0).toUpperCase()}</div>
                <div class="admin-user-info">
                    <div class="admin-user-name">${escapeHtml(u.username)} ${u.isBanned ? '🚫' : ''}</div>
                    <div class="admin-user-meta">${escapeHtml(u.email)} · ${escapeHtml(u.role)} · 💰${u.credits} · ⭐${u.reputationScore}</div>
                </div>
                <div class="admin-user-actions">
                    <select onchange="changeUserRole(${u.id},this.value)" style="background:var(--bg-input);border:1px solid var(--border);color:var(--text-primary);border-radius:var(--radius-sm);padding:0.2rem 0.4rem;font-size:11px">
                        <option value="Member" ${u.role==='Member'?'selected':''}>Member</option>
                        <option value="Moderator" ${u.role==='Moderator'?'selected':''}>Moderator</option>
                        <option value="Admin" ${u.role==='Admin'?'selected':''}>Admin</option>
                    </select>
                    ${u.isBanned 
                        ? `<button class="btn btn-sm btn-outline" onclick="unbanUser(${u.id})">Sbanna</button>`
                        : `<button class="btn btn-sm btn-danger" onclick="banUser(${u.id},'${escapeHtml(u.username)}')">Ban</button>`}
                    <button class="btn btn-sm btn-outline" onclick="adminCredits(${u.id},'${escapeHtml(u.username)}')">💰</button>
                    <button class="btn btn-sm btn-danger" onclick="deleteUser(${u.id},'${escapeHtml(u.username)}')">🗑️</button>
                </div>
            </div>`).join('')}
        <p style="color:var(--text-muted);font-size:11px;margin-top:0.8rem">${result.total} utenti totali</p>`;
}

function searchAdminUsers() {
    loadAdminUsers(document.getElementById('adminContent'), document.getElementById('adminUserSearch').value.trim());
}

async function changeUserRole(userId, role) {
    try { await api(`/admin/users/${userId}/role`, { method: 'PUT', body: JSON.stringify({ role }) }); }
    catch (err) { alert(err.data?.error || 'Errore'); loadAdmin(); }
}

async function deleteUser(userId, username) {
    if (!confirm(`Eliminare l'utente ${username}?`)) return;
    try { await api(`/admin/users/${userId}`, { method: 'DELETE' }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function banUser(userId, username) {
    const reason = prompt(`Motivo ban per ${username}:`);
    if (reason === null) return;
    try { await api(`/admin/users/${userId}/ban`, { method: 'PUT', body: JSON.stringify({ reason }) }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function unbanUser(userId) {
    try { await api(`/admin/users/${userId}/unban`, { method: 'PUT' }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

async function adminCredits(userId, username) {
    const amount = prompt(`Crediti da aggiungere/rimuovere per ${username} (es: 100 o -50):`);
    if (!amount) return;
    const reason = prompt('Motivo:') || 'Admin adjustment';
    try { await api(`/admin/users/${userId}/credits`, { method: 'PUT', body: JSON.stringify({ amount: parseInt(amount), reason }) }); loadAdmin(); }
    catch (err) { alert(err.data?.error || 'Errore'); }
}

// === Utility ===
function escapeHtml(str) {
    if (!str) return '';
    const d = document.createElement('div');
    d.textContent = str;
    return d.innerHTML;
}

function timeAgo(dateStr) {
    if (!dateStr) return '';
    const now = new Date();
    const date = new Date(dateStr);
    const secs = Math.floor((now - date) / 1000);
    if (secs < 60) return 'ora';
    const mins = Math.floor(secs / 60);
    if (mins < 60) return `${mins}m fa`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours}h fa`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days}g fa`;
    const months = Math.floor(days / 30);
    if (months < 12) return `${months}me fa`;
    return `${Math.floor(months / 12)}a fa`;
}

function formatFileSize(bytes) {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}

// === Follow ===
async function toggleFollow(userId) {
    if (!currentUser) { showModal('login'); return; }
    const btn = document.getElementById('followBtn');
    try {
        if (btn && btn.textContent.includes('Smetti')) {
            await api(`/users/${userId}/follow`, { method: 'DELETE' });
            showToast('Non segui più questo utente');
        } else {
            await api(`/users/${userId}/follow`, { method: 'POST' });
            showToast('Ora segui questo utente!');
        }
        loadProfile(userId);
    } catch (err) {
        showToast(err.data?.error || 'Errore', 'error');
    }
}

async function loadFollowersTab(userId) {
    const container = document.getElementById('profileTabFollowers');
    if (!container) return;
    try {
        const followers = await api(`/users/${userId}/followers`);
        if (followers.length === 0) {
            container.innerHTML = '<div class="empty-state"><p>Nessun follower</p></div>';
        } else {
            container.innerHTML = followers.map(f => `<div class="follow-card" onclick="navigate('profile',{id:${f.id}})">
                <div class="follow-avatar">${f.avatarUrl ? `<img src="${escapeHtml(f.avatarUrl)}" alt="">` : f.username.charAt(0).toUpperCase()}</div>
                <div class="follow-info">
                    <span class="follow-name">${escapeHtml(f.username)}</span>
                </div>
                <span class="follow-date">${timeAgo(f.followedAt)}</span>
            </div>`).join('');
        }
    } catch { container.innerHTML = '<div class="empty-state"><p>Errore</p></div>'; }
}

async function loadFollowingTab(userId) {
    const container = document.getElementById('profileTabFollowing');
    if (!container) return;
    try {
        const following = await api(`/users/${userId}/following`);
        if (following.length === 0) {
            container.innerHTML = '<div class="empty-state"><p>Non segue nessuno</p></div>';
        } else {
            container.innerHTML = following.map(f => `<div class="follow-card" onclick="navigate('profile',{id:${f.id}})">
                <div class="follow-avatar">${f.avatarUrl ? `<img src="${escapeHtml(f.avatarUrl)}" alt="">` : f.username.charAt(0).toUpperCase()}</div>
                <div class="follow-info">
                    <span class="follow-name">${escapeHtml(f.username)}</span>
                </div>
                <span class="follow-date">${timeAgo(f.followedAt)}</span>
            </div>`).join('');
        }
    } catch { container.innerHTML = '<div class="empty-state"><p>Errore</p></div>'; }
}

// === User Status ===
async function updateUserStatus(status) {
    try {
        await api('/users/status', { method: 'PUT', body: JSON.stringify({ status }) });
        showToast(`Stato aggiornato: ${status}`);
    } catch { showToast('Errore nel cambio stato', 'error'); }
}

// === Quote ===
// === WYSIWYG Toolbar ===
function wrapSelection(textareaId, before, after) {
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const start = ta.selectionStart;
    const end = ta.selectionEnd;
    const selected = ta.value.substring(start, end);
    ta.value = ta.value.substring(0, start) + before + selected + after + ta.value.substring(end);
    ta.focus();
    ta.selectionStart = start + before.length;
    ta.selectionEnd = start + before.length + selected.length;
}

function insertBBCode(textareaId, type) {
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const start = ta.selectionStart;
    let insert = '';
    if (type === 'url') {
        const url = prompt('Inserisci URL:');
        if (!url) return;
        const text = ta.value.substring(ta.selectionStart, ta.selectionEnd) || 'Link';
        insert = `[url=${url}]${text}[/url]`;
    } else if (type === 'img') {
        const url = prompt('Inserisci URL immagine:');
        if (!url) return;
        insert = `[img]${url}[/img]`;
    } else if (type === 'spoiler') {
        const selected = ta.value.substring(ta.selectionStart, ta.selectionEnd) || 'Contenuto nascosto';
        insert = `[spoiler]${selected}[/spoiler]`;
    }
    ta.value = ta.value.substring(0, start) + insert + ta.value.substring(ta.selectionEnd);
    ta.focus();
}

function showEmojiPicker(textareaId) {
    const emojis = ['😀','😂','🤣','😎','🤔','😮','😢','🔥','💀','👀','👍','👎','❤️','💯','⚡','🚀','💎','🎯','✅','❌'];
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const existing = document.getElementById('emojiPickerInline');
    if (existing) { existing.remove(); return; }
    const picker = document.createElement('div');
    picker.id = 'emojiPickerInline';
    picker.className = 'emoji-picker-inline';
    picker.innerHTML = emojis.map(e => `<button type="button" onclick="insertEmoji('${textareaId}','${e}')">${e}</button>`).join('');
    ta.parentElement.insertBefore(picker, ta);
}

function insertEmoji(textareaId, emoji) {
    const ta = document.getElementById(textareaId);
    if (!ta) return;
    const start = ta.selectionStart;
    ta.value = ta.value.substring(0, start) + emoji + ta.value.substring(ta.selectionEnd);
    ta.focus();
    ta.selectionStart = ta.selectionEnd = start + emoji.length;
    const picker = document.getElementById('emojiPickerInline');
    if (picker) picker.remove();
}

// === Toast Notifications ===
function showToast(message, type = 'success') {
    const container = document.getElementById('toastContainer');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `toast toast-${type} toast-enter`;
    toast.innerHTML = `<span class="toast-icon">${type === 'success' ? '✅' : type === 'error' ? '❌' : 'ℹ️'}</span><span>${escapeHtml(message)}</span>`;
    container.appendChild(toast);
    requestAnimationFrame(() => toast.classList.add('toast-visible'));
    setTimeout(() => {
        toast.classList.remove('toast-visible');
        toast.classList.add('toast-exit');
        setTimeout(() => toast.remove(), 400);
    }, 3000);
}

// === Theme Toggle ===
function toggleTheme() {
    const body = document.body;
    body.classList.toggle('light-theme');
    const isLight = body.classList.contains('light-theme');
    localStorage.setItem('crimecode_theme', isLight ? 'light' : 'dark');
    const btn = document.getElementById('themeToggle');
    if (btn) btn.textContent = isLight ? '☀️' : '🌙';
}

// Init theme from saved preference
(function initTheme() {
    const saved = localStorage.getItem('crimecode_theme');
    if (saved === 'light') {
        document.body.classList.add('light-theme');
        const btn = document.getElementById('themeToggle');
        if (btn) btn.textContent = '☀️';
    }
})();

// === Mobile Menu ===
function toggleMobileMenu() {
    const nav = document.getElementById('mainNav');
    const btn = document.getElementById('hamburgerBtn');
    nav.classList.toggle('open');
    btn.classList.toggle('active');
}

// Close mobile menu on link click
document.addEventListener('click', (e) => {
    if (e.target.closest('.main-nav .nav-link')) {
        const nav = document.getElementById('mainNav');
        const btn = document.getElementById('hamburgerBtn');
        if (nav) nav.classList.remove('open');
        if (btn) btn.classList.remove('active');
    }
});

// === Live Chat Widget ===
let chatCurrentUserId = null;
let chatLastMsgId = 0;
let chatPollInterval = null;

function toggleChatWidget() {
    const widget = document.getElementById('chatWidget');
    if (!widget) return;
    if (widget.style.display === 'none') {
        widget.style.display = 'flex';
        chatGoBack();
    } else {
        widget.style.display = 'none';
        stopChatPoll();
    }
}

function chatGoBack() {
    chatCurrentUserId = null;
    chatLastMsgId = 0;
    stopChatPoll();
    document.getElementById('chatWidgetTitle').textContent = '\ud83d\udcac Chat Live';
    document.getElementById('chatBackBtn').style.display = 'none';
    document.getElementById('chatWidgetInput').style.display = 'none';
    loadChatContacts();
}

async function loadChatContacts() {
    const body = document.getElementById('chatWidgetBody');
    body.innerHTML = '<div class="loading" style="padding:1rem">Caricamento...</div>';
    try {
        const contacts = await api('/chat/contacts');
        if (contacts.length === 0) {
            body.innerHTML = '<div class="empty-state" style="padding:1rem;text-align:center"><p style="color:var(--text-muted)">Nessun utente online</p></div>';
            return;
        }
        body.innerHTML = '<div class="chat-contacts-list">' + contacts.map(c => {
            const statusIcon = {online:'\ud83d\udfe2',away:'\ud83d\udfe1',busy:'\ud83d\udd34',offline:'\u26ab'}[c.status] || '\ud83d\udfe2';
            const avatarHtml = c.avatarUrl ? `<img src="${escapeHtml(c.avatarUrl)}" alt="">` : `<span>${c.username.charAt(0).toUpperCase()}</span>`;
            const unreadBadge = c.unreadCount > 0 ? `<span class="chat-contact-badge">${c.unreadCount}</span>` : '';
            const lastMsg = c.lastMessage ? `<div class="chat-contact-last">${escapeHtml(c.lastMessage)}</div>` : '';
            return `<div class="chat-contact-item" onclick="openChat(${c.userId},'${escapeHtml(c.username).replace(/'/g, "\\'")}')"> 
                <div class="chat-contact-avatar">${avatarHtml}</div>
                <div class="chat-contact-info">
                    <div class="chat-contact-name">${statusIcon} ${escapeHtml(c.username)} ${unreadBadge}</div>
                    ${lastMsg}
                </div>
            </div>`;
        }).join('') + '</div>';
    } catch {
        body.innerHTML = '<div class="empty-state" style="padding:1rem;text-align:center"><p style="color:var(--text-muted)">Errore</p></div>';
    }
}

async function openChat(userId, username) {
    chatCurrentUserId = userId;
    chatLastMsgId = 0;
    document.getElementById('chatWidgetTitle').textContent = escapeHtml(username);
    document.getElementById('chatBackBtn').style.display = '';
    document.getElementById('chatWidgetInput').style.display = 'flex';
    const body = document.getElementById('chatWidgetBody');
    body.innerHTML = '<div class="loading" style="padding:1rem">Caricamento...</div>';
    try {
        const msgs = await api(`/chat/${userId}`);
        renderChatMessages(msgs);
        startChatPoll();
        updateBadges();
    } catch {
        body.innerHTML = '<div class="empty-state" style="padding:1rem"><p>Errore</p></div>';
    }
    document.getElementById('chatMsgInput').focus();
}

function renderChatMessages(msgs) {
    const body = document.getElementById('chatWidgetBody');
    if (msgs.length === 0) {
        body.innerHTML = '<div class="chat-messages-empty"><p>Nessun messaggio. Scrivi per primo!</p></div>';
        return;
    }
    body.innerHTML = '<div class="chat-messages">' + msgs.map(m => {
        const isMine = m.senderId === currentUser.userId;
        chatLastMsgId = Math.max(chatLastMsgId, m.id);
        return `<div class="chat-msg ${isMine ? 'chat-msg-mine' : 'chat-msg-other'}">
            <div class="chat-msg-bubble">${escapeHtml(m.content)}</div>
            <div class="chat-msg-time">${timeAgo(m.createdAt)}</div>
        </div>`;
    }).join('') + '</div>';
    body.scrollTop = body.scrollHeight;
}

async function sendChatFromWidget() {
    if (!chatCurrentUserId) return;
    const input = document.getElementById('chatMsgInput');
    const content = input.value.trim();
    if (!content) return;
    input.value = '';
    try {
        const msg = await api(`/chat/${chatCurrentUserId}`, {
            method: 'POST',
            body: JSON.stringify({ content })
        });
        // Append this message to the chat
        const body = document.getElementById('chatWidgetBody');
        const container = body.querySelector('.chat-messages');
        if (container) {
            const div = document.createElement('div');
            div.className = 'chat-msg chat-msg-mine';
            div.innerHTML = `<div class="chat-msg-bubble">${escapeHtml(msg.content)}</div><div class="chat-msg-time">ora</div>`;
            container.appendChild(div);
            chatLastMsgId = Math.max(chatLastMsgId, msg.id);
            body.scrollTop = body.scrollHeight;
        } else {
            // First message in empty chat
            renderChatMessages([msg]);
        }
    } catch (err) {
        showToast(err.data?.error || 'Errore invio messaggio', 'error');
    }
}

function startChatPoll() {
    stopChatPoll();
    chatPollInterval = setInterval(async () => {
        if (!chatCurrentUserId) return;
        try {
            const newMsgs = await api(`/chat/${chatCurrentUserId}/new?afterId=${chatLastMsgId}`);
            if (newMsgs.length > 0) {
                const body = document.getElementById('chatWidgetBody');
                let container = body.querySelector('.chat-messages');
                if (!container) {
                    body.innerHTML = '<div class="chat-messages"></div>';
                    container = body.querySelector('.chat-messages');
                }
                for (const m of newMsgs) {
                    if (m.senderId === currentUser.userId) continue; // already shown
                    chatLastMsgId = Math.max(chatLastMsgId, m.id);
                    const div = document.createElement('div');
                    div.className = 'chat-msg chat-msg-other';
                    div.innerHTML = `<div class="chat-msg-bubble">${escapeHtml(m.content)}</div><div class="chat-msg-time">ora</div>`;
                    container.appendChild(div);
                }
                body.scrollTop = body.scrollHeight;
                updateBadges();
            }
        } catch { /* ignore */ }
    }, 3000);
}

function stopChatPoll() {
    if (chatPollInterval) {
        clearInterval(chatPollInterval);
        chatPollInterval = null;
    }
}

// Show chat toggle button when logged in
function updateChatVisibility() {
    const btn = document.getElementById('chatToggleBtn');
    if (btn) btn.style.display = currentUser ? '' : 'none';
    if (!currentUser) {
        const widget = document.getElementById('chatWidget');
        if (widget) widget.style.display = 'none';
        stopChatPoll();
    }
}

function openChatWith(userId, username) {
    const widget = document.getElementById('chatWidget');
    if (widget) widget.style.display = 'flex';
    openChat(userId, username);
}

// === Admin: Pending Listings ===
async function loadAdminListings(container) {
    const listings = await api('/admin/marketplace/pending');
    if (!listings || listings.length === 0) {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">✅</div><p>Nessun annuncio in attesa</p></div>';
        return;
    }
    container.innerHTML = listings.map(l => `
        <div class="admin-user-card">
            <div class="admin-user-info">
                <div class="admin-user-name">${escapeHtml(l.title)}</div>
                <div class="admin-user-meta">${escapeHtml(l.sellerName)} · ${l.priceCrypto} ${escapeHtml(l.currency)} · ${escapeHtml(l.categoryName)} · ${timeAgo(l.createdAt)}</div>
            </div>
            <div class="admin-user-actions">
                <button class="btn btn-sm btn-primary" onclick="adminReviewListing(${l.id},'Approved')">✅ Approva</button>
                <button class="btn btn-sm btn-danger" onclick="adminReviewListing(${l.id},'Rejected')">❌ Rifiuta</button>
            </div>
        </div>`).join('');
}

async function adminReviewListing(id, status) {
    let rejectionReason = null;
    if (status === 'Rejected') {
        rejectionReason = prompt('Motivo del rifiuto:');
        if (rejectionReason === null) return;
    }
    try {
        await api(`/admin/marketplace/${id}/review`, { method: 'PUT', body: JSON.stringify({ status, rejectionReason }) });
        showToast(status === 'Approved' ? 'Annuncio approvato!' : 'Annuncio rifiutato', 'success');
        loadAdmin();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Admin: Vendor Applications ===
async function loadAdminVendorApps(container) {
    const apps = await api('/admin/vendors/pending');
    if (!apps || apps.length === 0) {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">✅</div><p>Nessuna richiesta in attesa</p></div>';
        return;
    }
    container.innerHTML = apps.map(a => `
        <div class="admin-user-card">
            <div class="post-author-avatar" style="width:32px;height:32px;font-size:14px">${a.avatarUrl ? `<img src="${escapeHtml(a.avatarUrl)}" style="width:100%;height:100%;border-radius:50%">` : a.username.charAt(0).toUpperCase()}</div>
            <div class="admin-user-info">
                <div class="admin-user-name">${escapeHtml(a.username)}</div>
                <div class="admin-user-meta">📱 ${escapeHtml(a.telegramUsername)} · ${a.specialization ? escapeHtml(a.specialization) : 'N/A'}</div>
                <div class="admin-user-meta" style="margin-top:2px"><em>"${escapeHtml(a.motivation)}"</em></div>
            </div>
            <div class="admin-user-actions">
                <button class="btn btn-sm btn-primary" onclick="adminReviewVendor(${a.id},'Approved')">✅ Approva</button>
                <button class="btn btn-sm btn-danger" onclick="adminReviewVendor(${a.id},'Rejected')">❌ Rifiuta</button>
            </div>
        </div>`).join('');
}

async function adminReviewVendor(appId, status) {
    let reviewNote = null;
    if (status === 'Rejected') {
        reviewNote = prompt('Motivo del rifiuto:');
        if (reviewNote === null) return;
    }
    try {
        await api(`/vendor/applications/${appId}/review`, { method: 'PUT', body: JSON.stringify({ status, reviewNote }) });
        showToast(status === 'Approved' ? 'Venditore approvato!' : 'Richiesta rifiutata', 'success');
        loadAdmin();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Admin: Disputes ===
async function loadAdminDisputes(container) {
    const disputes = await api('/admin/disputes');
    if (!disputes || disputes.length === 0) {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">✅</div><p>Nessuna disputa attiva</p></div>';
        return;
    }
    container.innerHTML = disputes.map(d => `
        <div class="admin-user-card dispute-card">
            <div class="admin-user-info">
                <div class="admin-user-name">Ordine #${d.orderId} — ${escapeHtml(d.listingTitle)}</div>
                <div class="admin-user-meta">🛒 ${escapeHtml(d.buyerName)} → 🏪 ${escapeHtml(d.sellerName)} · ${d.amount} ${escapeHtml(d.currency)}</div>
                ${d.disputeReason ? `<div class="admin-user-meta" style="color:var(--accent-red)">⚠️ ${escapeHtml(d.disputeReason)}</div>` : ''}
            </div>
            <div class="admin-user-actions">
                <button class="btn btn-sm btn-primary" onclick="adminResolveDispute(${d.orderId},'ReleaseSeller')">💰 Rilascia al Venditore</button>
                <button class="btn btn-sm btn-danger" onclick="adminResolveDispute(${d.orderId},'RefundBuyer')">💸 Rimborsa Acquirente</button>
            </div>
        </div>`).join('');
}

async function adminResolveDispute(orderId, resolution) {
    const note = prompt('Nota sulla risoluzione:');
    if (note === null) return;
    try {
        await api(`/admin/disputes/${orderId}/resolve`, { method: 'PUT', body: JSON.stringify({ resolution, note }) });
        showToast('Disputa risolta!', 'success');
        loadAdmin();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Wallet ===
async function loadWallet() {
    const container = document.getElementById('walletContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const wallets = await api('/wallet');
        const txData = await api('/wallet/transactions');
        const cryptoIcons = { BTC: '₿', ETH: 'Ξ', USDT: '₮', LTC: 'Ł', XMR: 'ɱ' };
        container.innerHTML = `
            <div class="wallet-grid">
                ${['BTC','ETH','USDT','LTC','XMR'].map(c => {
                    const w = wallets.find(w => w.currency === c);
                    return `<div class="wallet-card">
                        <div class="wallet-currency">${c}</div>
                        <div class="wallet-balance">${w ? w.balance.toFixed(8) : '—'}</div>
                        ${w ? `
                            <div class="wallet-actions">
                                <button class="btn btn-sm btn-primary" onclick="walletDeposit('${c}')">📥 Deposita</button>
                                <button class="btn btn-sm btn-outline" onclick="walletWithdraw('${c}')">📤 Preleva</button>
                            </div>` : `
                            <button class="btn btn-sm btn-outline" onclick="walletInit('${c}')">⚡ Attiva Wallet</button>`}
                    </div>`;
                }).join('')}
            </div>
            <h3 style="margin-top:1.5rem;font-family:'Orbitron',sans-serif;font-size:0.95rem;letter-spacing:1px">📜 Transazioni recenti</h3>
            ${txData.transactions.length === 0 ? '<p style="color:var(--text-muted)">Nessuna transazione</p>' : `
                <div class="tx-list">
                    ${txData.transactions.map(t => `
                        <div class="tx-item ${t.amount > 0 ? 'tx-in' : 'tx-out'}">
                            <div class="tx-info">
                                <span class="tx-type">${t.type}</span>
                                <span class="tx-ref">${t.reference ? escapeHtml(t.reference) : '—'}</span>
                            </div>
                            <div class="tx-amount">${t.amount > 0 ? '+' : ''}${t.amount.toFixed(8)} ${escapeHtml(t.currency)}</div>
                            <div class="tx-date">${timeAgo(t.createdAt)}</div>
                        </div>`).join('')}
                </div>`}`;
    } catch (err) {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore caricamento wallet</p></div>';
    }
}

async function walletInit(currency) {
    try {
        await api(`/wallet/init?currency=${currency}`, { method: 'POST' });
        showToast(`Wallet ${currency} attivato!`, 'success');
        loadWallet();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function walletDeposit(currency) {
    const amount = prompt(`Importo da depositare (${currency}):`);
    if (!amount || isNaN(amount) || parseFloat(amount) <= 0) return;
    const txId = prompt('TX ID della transazione:');
    if (!txId) return;
    try {
        await api('/wallet/deposit', { method: 'POST', body: JSON.stringify({ currency, amount: parseFloat(amount), txId }) });
        showToast('Deposito registrato!', 'success');
        loadWallet();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function walletWithdraw(currency) {
    const amount = prompt(`Importo da prelevare (${currency}):`);
    if (!amount || isNaN(amount) || parseFloat(amount) <= 0) return;
    const walletAddress = prompt('Indirizzo wallet di destinazione:');
    if (!walletAddress) return;
    try {
        await api('/wallet/withdraw', { method: 'POST', body: JSON.stringify({ currency, amount: parseFloat(amount), walletAddress }) });
        showToast('Prelievo eseguito!', 'success');
        loadWallet();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Wishlist ===
async function loadWishlist() {
    const container = document.getElementById('wishlistContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const items = await api('/wishlist');
        if (!items || items.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">❤️</div><p>La tua wishlist è vuota.<br>Esplora il marketplace e salva i tuoi preferiti!</p></div>';
            return;
        }
        container.innerHTML = `
            <div class="marketplace-grid">${items.map(w => `
                <div class="marketplace-card" onclick="navigate('listingDetail',{id:${w.listingId}})">
                    ${w.listingImageUrl ? `<div class="marketplace-card-img"><img src="${escapeHtml(w.listingImageUrl)}" alt=""></div>` : ''}
                    <div class="marketplace-card-header">
                        <span class="marketplace-card-title">${escapeHtml(w.listingTitle)}</span>
                        <span class="marketplace-card-price">${w.priceCrypto} ${escapeHtml(w.currency)}</span>
                    </div>
                    <div class="marketplace-card-footer">
                        <span>${escapeHtml(w.sellerName)}</span>
                        <button class="btn btn-sm btn-danger" onclick="event.stopPropagation();toggleWishlist(${w.listingId})">❌ Rimuovi</button>
                    </div>
                </div>`).join('')}
            </div>`;
    } catch { container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>'; }
}

async function toggleWishlist(listingId) {
    if (!currentUser) { showModal('login'); return; }
    try {
        const res = await api('/wishlist/toggle', { method: 'POST', body: JSON.stringify({ listingId }) });
        showToast(res.inWishlist ? '❤️ Aggiunto alla wishlist' : '💔 Rimosso dalla wishlist', 'success');
        // Re-render if on wishlist/listing page
        const wishlistPage = document.getElementById('pageWishlist');
        if (wishlistPage && wishlistPage.style.display !== 'none') loadWishlist();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Vouchers ===
async function loadMyVouchers() {
    const container = document.getElementById('vouchersContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const vouchers = await api('/vouchers/my');
        container.innerHTML = `
            ${vouchers.length === 0 ? '<div class="empty-state"><div class="empty-icon">🎟️</div><p>Nessun voucher creato.<br>Crea il tuo primo voucher per offrire sconti!</p></div>' : `
                <div class="voucher-list">
                    ${vouchers.map(v => `
                        <div class="voucher-card ${!v.isActive ? 'inactive' : ''}">
                            <div class="voucher-code">${escapeHtml(v.code)}</div>
                            <div class="voucher-info">
                                <span>🏷️ -${v.discountPercent}%${v.maxDiscount ? ` (max ${v.maxDiscount})` : ''}</span>
                                <span>📊 Usi: ${v.usedCount}/${v.maxUses}</span>
                                ${v.listingTitle ? `<span>📦 ${escapeHtml(v.listingTitle)}</span>` : '<span>🌐 Tutti gli annunci</span>'}
                                ${v.expiresAt ? `<span>⏰ Scade: ${new Date(v.expiresAt).toLocaleDateString('it-IT')}</span>` : ''}
                            </div>
                            <div class="voucher-actions">
                                <button class="btn btn-sm ${v.isActive ? 'btn-outline' : 'btn-primary'}" onclick="toggleVoucher(${v.id})">${v.isActive ? '⏸️ Disattiva' : '▶️ Attiva'}</button>
                                <button class="btn btn-sm btn-danger" onclick="deleteVoucher(${v.id})">🗑️</button>
                            </div>
                        </div>`).join('')}
                </div>`}`;
    } catch { container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>'; }
}

function showCreateVoucherModal() {
    showModal('createVoucher');
}

async function createVoucher(e) {
    e.preventDefault();
    const errEl = document.getElementById('voucherError');
    errEl.textContent = '';
    try {
        await api('/vouchers', { method: 'POST', body: JSON.stringify({
            code: document.getElementById('voucherCode').value.trim(),
            discountPercent: parseFloat(document.getElementById('voucherDiscount').value),
            maxDiscount: document.getElementById('voucherMaxDiscount').value ? parseFloat(document.getElementById('voucherMaxDiscount').value) : null,
            maxUses: parseInt(document.getElementById('voucherMaxUses').value) || 100,
            expiresAt: document.getElementById('voucherExpiry').value || null,
            listingId: document.getElementById('voucherListing').value ? parseInt(document.getElementById('voucherListing').value) : null
        })});
        closeModal();
        showToast('Voucher creato!', 'success');
        loadMyVouchers();
    } catch (err) { errEl.textContent = err.data?.error || 'Errore'; }
}

async function toggleVoucher(id) {
    try { await api(`/vouchers/${id}/toggle`, { method: 'PUT' }); loadMyVouchers(); }
    catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function deleteVoucher(id) {
    if (!confirm('Eliminare questo voucher?')) return;
    try { await api(`/vouchers/${id}`, { method: 'DELETE' }); loadMyVouchers(); }
    catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Vendor Stats ===
async function loadVendorStats() {
    const container = document.getElementById('vendorStatsContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const s = await api('/vendor-stats');
        container.innerHTML = `
            <div class="admin-stats">
                <div class="admin-stat"><div class="stat-value">🛒 ${s.totalSales}</div><div class="stat-label">Vendite Totali</div></div>
                <div class="admin-stat"><div class="stat-value">💰 ${s.totalRevenue.toFixed(4)}</div><div class="stat-label">Ricavo Totale</div></div>
                <div class="admin-stat"><div class="stat-value">📦 ${s.activeListings}</div><div class="stat-label">Annunci Attivi</div></div>
                <div class="admin-stat"><div class="stat-value">⏳ ${s.pendingOrders}</div><div class="stat-label">Ordini Pending</div></div>
                <div class="admin-stat"><div class="stat-value">⚠️ ${s.disputedOrders}</div><div class="stat-label">Dispute</div></div>
                <div class="admin-stat"><div class="stat-value">${s.averageRating}⭐</div><div class="stat-label">${s.totalReviews} Recensioni</div></div>
            </div>
            <h3 style="margin-top:1.5rem;font-family:'Orbitron',sans-serif;font-size:0.95rem;letter-spacing:1px">📈 Ultimi 6 mesi</h3>
            <div class="monthly-stats">
                ${s.last6Months.map(m => `
                    <div class="monthly-stat">
                        <div class="monthly-label">${escapeHtml(m.month)}</div>
                        <div class="monthly-bar" style="height:${Math.max(m.sales * 10, 4)}px"></div>
                        <div class="monthly-value">${m.sales} vendite · ${m.revenue.toFixed(4)}</div>
                    </div>`).join('')}
            </div>
            <div class="admin-stats" style="margin-top:1rem">
                <div class="admin-stat"><div class="stat-value">${s.monthlySales}</div><div class="stat-label">Vendite Mese</div></div>
                <div class="admin-stat"><div class="stat-value">${s.monthlyRevenue.toFixed(4)}</div><div class="stat-label">Ricavo Mese</div></div>
            </div>`;
    } catch { container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore o non sei un venditore</p></div>'; }
}

// === Reviews ===
async function submitReview(orderId) {
    const rating = parseInt(document.getElementById('reviewRating').value);
    const comment = document.getElementById('reviewComment').value.trim();
    if (rating < 1 || rating > 5) return showToast('Rating deve essere da 1 a 5', 'error');
    try {
        await api('/reviews', { method: 'POST', body: JSON.stringify({ orderId, rating, comment: comment || null }) });
        showToast('Recensione inviata!', 'success');
        loadOrderDetail(orderId);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === 2FA Settings ===
async function load2FASettings() {
    const container = document.getElementById('twofaContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const status = await api('/2fa/status');
        if (status.enabled) {
            container.innerHTML = `
                <div class="twofa-status enabled">
                    <div class="status-badge enabled">🛡️ ATTIVO</div>
                    <p style="color:var(--text-secondary);margin-bottom:0.5rem">Il tuo account è protetto con autenticazione a due fattori.</p>
                    <div class="form-group">
                        <label>Per disattivare, inserisci il codice attuale:</label>
                        <input type="text" id="disable2FACode" placeholder="000000" maxlength="6">
                        <button class="btn btn-danger btn-sm" onclick="disable2FA()" style="margin-top:0.8rem;width:100%">🔓 Disattiva 2FA</button>
                    </div>
                </div>`;
        } else {
            container.innerHTML = `
                <div class="twofa-status disabled">
                    <div class="status-badge disabled">⚠️ NON ATTIVO</div>
                    <p style="color:var(--text-secondary);margin-bottom:1rem">Proteggi il tuo account attivando l'autenticazione a due fattori.</p>
                    <button class="btn btn-primary" onclick="setup2FA()" style="padding:0.6rem 2rem">🔐 Configura 2FA</button>
                </div>
                <div id="twofaSetup"></div>`;
        }
    } catch { container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>'; }
}

async function setup2FA() {
    try {
        const res = await api('/2fa/setup', { method: 'POST' });
        document.getElementById('twofaSetup').innerHTML = `
            <div class="twofa-setup-box">
                <h3>Scansiona questo QR Code con la tua app (Google Authenticator, Authy, ecc.)</h3>
                <div class="twofa-secret"><code>${escapeHtml(res.secret)}</code></div>
                <p style="color:var(--text-muted);font-size:11px">URI: ${escapeHtml(res.qrCodeUri)}</p>
                <div class="form-group" style="margin-top:1rem">
                    <label>Inserisci il codice generato per verificare:</label>
                    <input type="text" id="verify2FACode" placeholder="123456" maxlength="6">
                    <button class="btn btn-primary btn-sm" onclick="enable2FA()" style="margin-top:0.5rem">Verifica e Attiva</button>
                </div>
            </div>`;
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function enable2FA() {
    const code = document.getElementById('verify2FACode').value.trim();
    if (!code) return showToast('Inserisci il codice', 'error');
    try {
        await api('/2fa/enable', { method: 'POST', body: JSON.stringify({ code }) });
        showToast('2FA attivato con successo!', 'success');
        load2FASettings();
    } catch (err) { showToast(err.data?.error || 'Codice non valido', 'error'); }
}

async function disable2FA() {
    const code = document.getElementById('disable2FACode').value.trim();
    if (!code) return showToast('Inserisci il codice', 'error');
    try {
        await api('/2fa/disable', { method: 'POST', body: JSON.stringify({ code }) });
        showToast('2FA disattivato', 'success');
        load2FASettings();
    } catch (err) { showToast(err.data?.error || 'Codice non valido', 'error'); }
}

// === Support Tickets ===
async function loadTickets() {
    const container = document.getElementById('ticketsContent');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const tickets = await api('/tickets/my');
        if (!tickets || tickets.length === 0) {
            container.innerHTML = '<div class="empty-state"><div class="empty-icon">🎫</div><p>Nessun ticket aperto</p><button class="btn btn-primary" onclick="showCreateTicketForm()">Crea il tuo primo ticket</button></div>';
            return;
        }
        container.innerHTML = tickets.map(t => `
            <div class="ticket-card ticket-status-${t.status.toLowerCase()}" onclick="navigate('ticketDetail',{id:${t.id}})">
                <div class="ticket-card-header">
                    <span class="ticket-id">#${t.id}</span>
                    <span class="ticket-status-badge">${getTicketStatusIcon(t.status)} ${t.status}</span>
                </div>
                <div class="ticket-card-title">${escapeHtml(t.subject)}</div>
                <div class="ticket-card-meta">
                    <span>📁 ${escapeHtml(t.category)}</span>
                    <span>💬 ${t.replyCount} risposte</span>
                    <span>📅 ${timeAgo(t.createdAt)}</span>
                </div>
            </div>
        `).join('');
    } catch { container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore caricamento</p></div>'; }
}

function getTicketStatusIcon(s) {
    return { Open: '🟢', InProgress: '🟡', Resolved: '✅', Closed: '⚫' }[s] || '❓';
}

function showCreateTicketForm() {
    const container = document.getElementById('ticketsContent');
    container.innerHTML = `
        <div class="ticket-form">
            <h3>📝 Nuovo Ticket</h3>
            <label>Oggetto</label>
            <input type="text" id="ticketSubject" maxlength="200" placeholder="Descrivi brevemente il problema">
            <label>Categoria</label>
            <select id="ticketCategory">
                <option value="General">Generale</option>
                <option value="Order">Ordine</option>
                <option value="Account">Account</option>
                <option value="Bug">Bug/Problema</option>
                <option value="Other">Altro</option>
            </select>
            <label>Messaggio</label>
            <textarea id="ticketMessage" rows="5" maxlength="2000" placeholder="Descrivi il problema in dettaglio..."></textarea>
            <div style="display:flex;gap:0.5rem;margin-top:0.8rem">
                <button class="btn btn-primary" onclick="submitTicket()">Invia Ticket</button>
                <button class="btn btn-outline" onclick="loadTickets()">Annulla</button>
            </div>
        </div>`;
}

async function submitTicket() {
    const subject = document.getElementById('ticketSubject').value.trim();
    const message = document.getElementById('ticketMessage').value.trim();
    const category = document.getElementById('ticketCategory').value;
    if (!subject || !message) return showToast('Compila tutti i campi', 'error');
    try {
        await api('/tickets', { method: 'POST', body: JSON.stringify({ subject, message, category }) });
        showToast('Ticket creato', 'success');
        loadTickets();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function loadTicketDetail(id) {
    const container = document.getElementById('ticketDetail');
    container.innerHTML = '<div class="loading">Caricamento</div>';
    try {
        const t = await api(`/tickets/${id}`);
        let html = `
            <span style="display:inline-block;margin-bottom:0.8rem;cursor:pointer;color:var(--accent);font-size:13px" onclick="navigate('tickets')">← Torna ai ticket</span>
            <div class="ticket-detail-header">
                <h2>${escapeHtml(t.subject)}</h2>
                <div class="ticket-detail-meta">
                    <span class="ticket-status-badge">${getTicketStatusIcon(t.status)} ${t.status}</span>
                    <span>📁 ${escapeHtml(t.category)}</span>
                    <span>⚡ ${t.priority}</span>
                    <span>📅 ${timeAgo(t.createdAt)}</span>
                </div>
            </div>
            <div class="ticket-message ticket-original">
                <div class="ticket-message-author">${escapeHtml(t.username)}</div>
                <div class="ticket-message-content">${escapeHtml(t.message)}</div>
                <div class="ticket-message-date">${new Date(t.createdAt).toLocaleString('it-IT')}</div>
            </div>`;

        if (t.replies && t.replies.length) {
            html += t.replies.map(r => `
                <div class="ticket-message ${r.isStaff ? 'ticket-staff-reply' : ''}">
                    <div class="ticket-message-author">${r.isStaff ? '🛡️ ' : ''}${escapeHtml(r.authorName)}</div>
                    <div class="ticket-message-content">${escapeHtml(r.message)}</div>
                    <div class="ticket-message-date">${new Date(r.createdAt).toLocaleString('it-IT')}</div>
                </div>
            `).join('');
        }

        if (t.status !== 'Closed') {
            html += `
                <div class="ticket-reply-form">
                    <textarea id="ticketReplyMsg" rows="3" placeholder="Scrivi una risposta..." maxlength="2000"></textarea>
                    <div style="display:flex;gap:0.5rem;margin-top:0.5rem">
                        <button class="btn btn-primary btn-sm" onclick="replyTicket(${id})">Rispondi</button>
                        <button class="btn btn-outline btn-sm" onclick="closeTicket(${id})">Chiudi Ticket</button>
                    </div>
                </div>`;
        }

        container.innerHTML = html;
    } catch { container.innerHTML = '<div class="empty-state"><div class="empty-icon">❌</div><p>Errore</p></div>'; }
}

async function replyTicket(id) {
    const msg = document.getElementById('ticketReplyMsg').value.trim();
    if (!msg) return showToast('Scrivi un messaggio', 'error');
    try {
        await api(`/tickets/${id}/reply`, { method: 'POST', body: JSON.stringify({ message: msg }) });
        showToast('Risposta inviata', 'success');
        loadTicketDetail(id);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function closeTicket(id) {
    try {
        await api(`/tickets/${id}/close`, { method: 'PUT' });
        showToast('Ticket chiuso', 'success');
        loadTicketDetail(id);
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Admin Analytics ===
async function loadAdminAnalytics(container) {
    const d = await api('/analytics/dashboard');
    container.innerHTML = `
        <div class="analytics-dashboard">
            <div class="analytics-totals">
                <div class="analytics-card"><div class="analytics-value">${d.totalUsers}</div><div class="analytics-label">👥 Utenti totali</div></div>
                <div class="analytics-card"><div class="analytics-value">${d.totalListings}</div><div class="analytics-label">🏪 Annunci</div></div>
                <div class="analytics-card"><div class="analytics-value">${d.totalSales}</div><div class="analytics-label">🛒 Vendite</div></div>
                <div class="analytics-card"><div class="analytics-value">${d.totalOrders}</div><div class="analytics-label">📦 Ordini</div></div>
                <div class="analytics-card"><div class="analytics-value">${d.totalRevenue.toFixed(2)}</div><div class="analytics-label">💰 Revenue</div></div>
                <div class="analytics-card"><div class="analytics-value">${d.openTickets}</div><div class="analytics-label">🎫 Ticket aperti</div></div>
            </div>

            <div class="analytics-section">
                <h3>📅 Oggi</h3>
                <div class="analytics-mini-stats">
                    <span>👥 +${d.todayUsers} utenti</span>
                    <span>🏪 +${d.todayListings} annunci</span>
                    <span>📦 +${d.todayOrders} ordini</span>
                </div>
            </div>

            <div class="analytics-section">
                <h3>📊 Ultimi 30 giorni — Nuovi utenti</h3>
                <div class="analytics-chart">
                    ${renderBarChart(d.dailyUsers)}
                </div>
            </div>

            <div class="analytics-section">
                <h3>🏪 Ultimi 30 giorni — Annunci</h3>
                <div class="analytics-chart">
                    ${renderBarChart(d.dailyListings)}
                </div>
            </div>

            <div class="analytics-section">
                <h3>📦 Ultimi 30 giorni — Ordini</h3>
                <div class="analytics-chart">
                    ${renderBarChart(d.dailyOrders)}
                </div>
            </div>

            ${d.topVendors?.length ? `<div class="analytics-section">
                <h3>🏆 Top Venditori</h3>
                <div class="user-list">
                    ${d.topVendors.map((v, i) => `<div class="user-card"><div class="user-card-body"><div class="user-card-title">${i + 1}. ${escapeHtml(v.username)}</div><div class="user-card-meta"><span>📦 ${v.totalSales} vendite · 💰 ${v.totalRevenue.toFixed(2)}</span></div></div></div>`).join('')}
                </div>
            </div>` : ''}
        </div>`;
}

function renderBarChart(dataPoints) {
    if (!dataPoints || !dataPoints.length) return '<p class="text-muted">Nessun dato</p>';
    const max = Math.max(...dataPoints.map(d => d.count), 1);
    return `<div class="bar-chart">${dataPoints.map(d => {
        const pct = (d.count / max * 100);
        const label = new Date(d.date).toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' });
        return `<div class="bar-col" title="${label}: ${d.count}"><div class="bar" style="height:${Math.max(pct, 2)}%"></div><span class="bar-label">${label}</span></div>`;
    }).join('')}</div>`;
}

// === Admin Logs ===
async function loadAdminLogs(container, page = 1) {
    const result = await api(`/analytics/logs?page=${page}&pageSize=30`);
    if (!result.logs || result.logs.length === 0) {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">📜</div><p>Nessun log</p></div>';
        return;
    }
    container.innerHTML = `
        <div class="admin-logs-list">
            ${result.logs.map(l => `
                <div class="admin-log-item">
                    <div class="admin-log-action">${getLogIcon(l.action)} <strong>${escapeHtml(l.action)}</strong></div>
                    <div class="admin-log-details">${escapeHtml(l.details)}</div>
                    <div class="admin-log-meta">
                        <span>👤 ${escapeHtml(l.adminName)}</span>
                        ${l.targetType ? `<span>🎯 ${escapeHtml(l.targetType)} #${l.targetId || ''}</span>` : ''}
                        <span>📅 ${new Date(l.createdAt).toLocaleString('it-IT')}</span>
                    </div>
                </div>
            `).join('')}
        </div>
        ${result.total > 30 ? `<div class="pagination" style="margin-top:1rem">
            ${page > 1 ? `<button class="btn btn-outline btn-sm" onclick="loadAdminLogs(document.getElementById('adminContent'),${page - 1})">← Prec</button>` : ''}
            <span>Pagina ${page} di ${Math.ceil(result.total / 30)}</span>
            ${page * 30 < result.total ? `<button class="btn btn-outline btn-sm" onclick="loadAdminLogs(document.getElementById('adminContent'),${page + 1})">Succ →</button>` : ''}
        </div>` : ''}`;
}

function getLogIcon(action) {
    const icons = { BanUser: '🚫', UnbanUser: '✅', ChangeRole: '🔄', DeleteUser: '🗑️', ModifyCredits: '💰', ReviewListing: '🏪', ResolveDispute: '⚖️' };
    return icons[action] || '📋';
}

// === Admin Tickets ===
async function loadAdminTickets(container) {
    const tickets = await api('/tickets/all');
    if (!tickets || tickets.length === 0) {
        container.innerHTML = '<div class="empty-state"><div class="empty-icon">🎫</div><p>Nessun ticket</p></div>';
        return;
    }
    container.innerHTML = `
        <div class="admin-tickets-filters" style="margin-bottom:1rem;display:flex;gap:0.5rem;flex-wrap:wrap">
            <button class="btn btn-outline btn-sm" onclick="filterAdminTickets('')">Tutti (${tickets.length})</button>
            <button class="btn btn-outline btn-sm" onclick="filterAdminTickets('Open')">🟢 Open</button>
            <button class="btn btn-outline btn-sm" onclick="filterAdminTickets('InProgress')">🟡 InProgress</button>
            <button class="btn btn-outline btn-sm" onclick="filterAdminTickets('Resolved')">✅ Resolved</button>
        </div>
        <div id="adminTicketsList">
            ${renderAdminTicketsList(tickets)}
        </div>`;
}

function renderAdminTicketsList(tickets) {
    return tickets.map(t => `
        <div class="ticket-card ticket-status-${t.status.toLowerCase()}">
            <div class="ticket-card-header">
                <span class="ticket-id">#${t.id}</span>
                <span>👤 ${escapeHtml(t.username)}</span>
                <span class="ticket-status-badge">${getTicketStatusIcon(t.status)} ${t.status}</span>
                <span>⚡ ${t.priority}</span>
            </div>
            <div class="ticket-card-title">${escapeHtml(t.subject)}</div>
            <div class="ticket-card-meta">
                <span>📁 ${escapeHtml(t.category)}</span>
                <span>📅 ${timeAgo(t.createdAt)}</span>
            </div>
            <div class="ticket-admin-actions" style="margin-top:0.5rem;display:flex;gap:0.5rem;flex-wrap:wrap">
                <select onchange="updateTicketStatus(${t.id},this.value)" style="background:var(--bg-input);border:1px solid var(--border);color:var(--text-primary);border-radius:var(--radius-sm);padding:0.2rem 0.4rem;font-size:11px">
                    <option value="" disabled selected>Stato...</option>
                    <option value="Open">Open</option>
                    <option value="InProgress">InProgress</option>
                    <option value="Resolved">Resolved</option>
                    <option value="Closed">Closed</option>
                </select>
                <select onchange="updateTicketPriority(${t.id},this.value)" style="background:var(--bg-input);border:1px solid var(--border);color:var(--text-primary);border-radius:var(--radius-sm);padding:0.2rem 0.4rem;font-size:11px">
                    <option value="" disabled selected>Priorità...</option>
                    <option value="Low">Low</option>
                    <option value="Normal">Normal</option>
                    <option value="High">High</option>
                    <option value="Urgent">Urgent</option>
                </select>
                <button class="btn btn-outline btn-sm" style="font-size:11px" onclick="navigate('ticketDetail',{id:${t.id}})">👁️ Apri</button>
            </div>
        </div>
    `).join('');
}

async function filterAdminTickets(status) {
    const url = status ? `/tickets/all?status=${status}` : '/tickets/all';
    const tickets = await api(url);
    document.getElementById('adminTicketsList').innerHTML = renderAdminTicketsList(tickets);
}

async function updateTicketStatus(id, status) {
    try {
        await api(`/tickets/${id}/update`, { method: 'PUT', body: JSON.stringify({ status }) });
        showToast('Stato aggiornato', 'success');
        loadAdmin();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

async function updateTicketPriority(id, priority) {
    try {
        await api(`/tickets/${id}/update`, { method: 'PUT', body: JSON.stringify({ priority }) });
        showToast('Priorità aggiornata', 'success');
        loadAdmin();
    } catch (err) { showToast(err.data?.error || 'Errore', 'error'); }
}

// === Export Orders CSV ===
async function exportOrdersCsv() {
    const activeTab = document.querySelector('.orders-tabs button.active');
    const type = activeTab && activeTab.textContent.includes('Vendite') ? 'selling' : 'buying';
    try {
        const response = await fetch(`/api/export/orders?type=${type}`, {
            headers: { 'Authorization': `Bearer ${currentUser.token}` }
        });
        if (!response.ok) throw new Error('Errore export');
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `orders_${type}_${new Date().toISOString().split('T')[0]}.csv`;
        a.click();
        URL.revokeObjectURL(url);
        showToast('Export completato', 'success');
    } catch { showToast('Errore durante l\'export', 'error'); }
}

// === Service Worker Registration ===
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/service-worker.js').catch(() => {});
}
