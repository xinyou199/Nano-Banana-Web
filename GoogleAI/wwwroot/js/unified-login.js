// 统一登录页面JavaScript逻辑
// 整合微信扫码登录和账号密码登录

// 全局变量
let currentSessionId = null;
let pollingTimer = null;
let statusCheckCount = 0;
const MAX_STATUS_CHECKS = 300; // 5分钟，每秒检查一次
let currentLoginMethod = 'wechat'; // 默认微信登录

// 页面加载完成后初始化
document.addEventListener('DOMContentLoaded', function () {
    initUnifiedLogin();
});

// 初始化统一登录
function initUnifiedLogin() {
    // 生成粒子效果
    createParticles();

    // 初始化登录方式切换
    initLoginMethodSwitcher();

    // 默认显示微信登录
    switchToLoginMethod('wechat');

    // 绑定表单事件
    bindFormEvents();

    // 页面加载完成后立即初始化微信登录（无论当前显示哪个面板）
    setTimeout(() => {
        if (document.getElementById('wechat-login-panel').classList.contains('active')) {
            initQrLogin();
        }
    }, 100);
}

// 生成粒子效果
function createParticles() {
    const container = document.getElementById('particles');
    if (!container) return;

    const particleCount = window.innerWidth < 480 ? 15 : 30;

    for (let i = 0; i < particleCount; i++) {
        const particle = document.createElement('div');
        particle.className = 'particle';
        particle.style.left = Math.random() * 100 + '%';
        particle.style.animationDelay = Math.random() * 15 + 's';
        particle.style.animationDuration = (10 + Math.random() * 10) + 's';
        particle.style.opacity = 0.2 + Math.random() * 0.5;
        particle.style.width = (1 + Math.random() * 2) + 'px';
        particle.style.height = particle.style.width;
        container.appendChild(particle);
    }
}

// 初始化登录方式切换器
function initLoginMethodSwitcher() {
    const switcherBtns = document.querySelectorAll('.switcher-btn');

    switcherBtns.forEach(btn => {
        btn.addEventListener('click', function () {
            const method = this.getAttribute('data-method');
            switchToLoginMethod(method);
        });
    });
}

// 切换到指定登录方式
function switchToLoginMethod(method) {
    if (method === currentLoginMethod) return;

    // 更新按钮状态
    document.querySelectorAll('.switcher-btn').forEach(btn => {
        btn.classList.remove('active');
        if (btn.getAttribute('data-method') === method) {
            btn.classList.add('active');
        }
    });

    // 切换面板显示
    document.querySelectorAll('.login-panel').forEach(panel => {
        panel.classList.remove('active');
    });

    const targetPanel = document.getElementById(`${method}-login-panel`);
    if (targetPanel) {
        targetPanel.classList.add('active');
    }

    // 更新当前登录方式
    currentLoginMethod = method;

    // 清除错误信息
    hideError();

    // 如果切换到微信登录，初始化微信登录
    if (method === 'wechat') {
        // 延迟初始化，确保面板已显示
        setTimeout(() => {
            // 检查是否已经初始化过，避免重复创建会话
            if (!currentSessionId) {
                initQrLogin();
            }
        }, 150);
    }
}

// 绑定表单事件
function bindFormEvents() {
    // 密码登录表单提交
    const loginForm = document.getElementById('login-form');
    if (loginForm) {
        loginForm.addEventListener('submit', handlePasswordLogin);
    }

    // 输入框回车事件
    const usernameInput = document.getElementById('username');
    const passwordInput = document.getElementById('password');

    if (usernameInput) {
        usernameInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                passwordInput.focus();
            }
        });
    }

    if (passwordInput) {
        passwordInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                handlePasswordLogin(e);
            }
        });
    }
}

// 处理密码登录
async function handlePasswordLogin(e) {
    e.preventDefault();

    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value.trim();
    const rememberMe = document.getElementById('rememberMe').checked;

    if (!username || !password) {
        showError('用户名和密码不能为空');
        return;
    }

    const loginBtn = document.getElementById('login-btn');
    loginBtn.disabled = true;
    loginBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> 登录中...';

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                username,
                password,
                rememberMe
            })
        });

        const result = await response.json();

        if (result.success) {
            // 保存认证信息
            saveAuthInfo(result);

            // 登录成功，跳转到绘图页面
            showSuccessMessage('登录成功，正在跳转...');
            setTimeout(() => {
                window.location.href = '/Home/Drawing';
            }, 1000);
        } else {
            showError(result.message || '登录失败，请检查用户名和密码');
        }
    } catch (error) {
        console.error('登录请求失败:', error);
        showError('登录失败: ' + (error.message || '网络连接异常'));
    } finally {
        loginBtn.disabled = false;
        loginBtn.innerHTML = '<i class="fas fa-sign-in-alt"></i> 登录';
    }
}

// 保存认证信息
function saveAuthInfo(authData) {
    if (authData.token) {
        localStorage.setItem('token', authData.token);
        // 同时保存到Cookie，供服务端验证使用
        document.cookie = `token=${authData.token}; path=/; max-age=86400`; // 24小时有效期
    }

    if (authData.user) {
        localStorage.setItem('user', JSON.stringify(authData.user));
        localStorage.setItem('userId', authData.user.id);
        localStorage.setItem('username', authData.user.username);
    }
}

// 显示成功消息
function showSuccessMessage(message) {
    // 创建临时成功提示
    const successDiv = document.createElement('div');
    successDiv.className = 'alert alert-success';
    successDiv.style.cssText = 'display: block; margin-bottom: 16px; background: rgba(34, 197, 94, 0.1); color: #86efac; border: 1px solid rgba(34, 197, 94, 0.2);';
    successDiv.textContent = message;

    const form = document.getElementById('login-form');
    form.insertBefore(successDiv, form.firstChild);

    // 3秒后移除
    setTimeout(() => {
        if (successDiv.parentNode) {
            successDiv.parentNode.removeChild(successDiv);
        }
    }, 3000);
}

// ==================== 微信扫码登录逻辑 ====================

// 初始化扫码登录
async function initQrLogin() {
    try {
        updateStatus('正在获取登录二维码...', 'info');
        await createQrSession();
    } catch (error) {
        console.error('初始化扫码登录失败:', error);
        showError('获取登录二维码失败，请刷新页面重试');
    }
}

// 创建扫码会话
async function createQrSession() {
    try {
        const response = await fetch('/api/wechatminiprogram/qr-session', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        const data = await response.json();

        if (!data.success) {
            throw new Error(data.message || '创建会话失败');
        }

        currentSessionId = data.sessionId;

        // 显示二维码
        document.getElementById('qrCodeLoading').style.display = 'none';
        document.getElementById('qrCodeArea').style.display = 'block';
        document.getElementById('qrCodeImage').src = data.qrCodeUrl;

        updateStatus('请使用微信扫描二维码', 'waiting');

        // 开始轮询登录状态
        startPollingStatus();

    } catch (error) {
        console.error('创建扫码会话失败:', error);
        showError('创建登录会话失败: ' + error.message);
    }
}

// 开始轮询登录状态
function startPollingStatus() {
    statusCheckCount = 0;
    pollingTimer = setInterval(checkLoginStatus, 2000); // 每2秒检查一次
}

// 检查登录状态
async function checkLoginStatus() {
    if (!currentSessionId || statusCheckCount >= MAX_STATUS_CHECKS) {
        stopPolling();
        if (statusCheckCount >= MAX_STATUS_CHECKS) {
            handleTimeout();
        }
        return;
    }

    statusCheckCount++;

    try {
        const response = await fetch(`/api/wechatminiprogram/login-status/${currentSessionId}`);
        const data = await response.json();

        if (!data.success) {
            // 会话不存在或已过期
            stopPolling();
            updateStatus('二维码已过期，请刷新', 'error');
            document.getElementById('qrCodeStatus').innerHTML = `
                <i class="fas fa-exclamation-circle"></i>
                <span>二维码已过期</span>
            `;
            return;
        }

        // 根据状态更新UI
        switch (data.status) {
            case 'pending':
                // 等待扫码
                break;
            case 'scanned':
                // 已扫码
                updateStatus('已扫码，请在手机上确认授权', 'scanned');
                document.getElementById('qrCodeStatus').innerHTML = `
                    <i class="fas fa-check-circle"></i>
                    <span>已扫码，请确认</span>
                `;
                break;
            case 'authorized':
                // 已授权
                updateStatus('授权成功，正在登录...', 'success');
                break;
            case 'cancelled':
                // 取消授权
                stopPolling();
                updateStatus('已取消授权，请刷新重试', 'cancelled');
                document.getElementById('qrCodeStatus').innerHTML = `
                    <i class="fas fa-times-circle"></i>
                    <span>已取消授权</span>
                `;
                break;
            case 'timeout':
                // 超时
                handleTimeout();
                break;
            case 'completed':
                stopPolling();
                if (data.token) {
                    await handleLoginSuccess(data);
                } else {
                    console.error('Token不存在!');
                }
                break;
        }

    } catch (error) {
        console.error('检查登录状态失败:', error);
        // 网络错误不停止轮询，继续尝试
    }
}

// 处理登录成功
async function handleLoginSuccess(data) {
    try {
        // 保存认证信息
        saveAuthInfo(data);

        updateStatus('登录成功，正在跳转...', 'success');

        setTimeout(() => {
            window.location.href = '/Home/Drawing';
        }, 500);

    } catch (error) {
        console.error('处理登录成功失败:', error);
        showError('登录成功但保存信息失败: ' + error.message);
    }
}

// 处理超时
function handleTimeout() {
    stopPolling();
    updateStatus('二维码已过期，请刷新二维码', 'timeout');
    document.getElementById('qrCodeStatus').innerHTML = `
        <i class="fas fa-clock"></i>
        <span>二维码已过期</span>
    `;
}

// 停止轮询
function stopPolling() {
    if (pollingTimer) {
        clearInterval(pollingTimer);
        pollingTimer = null;
    }
}

// 刷新二维码
function refreshQrCode() {
    stopPolling();

    // 重置UI
    document.getElementById('qrCodeLoading').style.display = 'block';
    document.getElementById('qrCodeArea').style.display = 'none';

    // 重置会话ID
    currentSessionId = null;

    // 重新创建会话
    createQrSession();
}

// 更新状态提示
function updateStatus(message, type) {
    const statusText = document.getElementById('statusText');
    const statusBar = document.querySelector('.qr-status-bar');

    if (statusText) {
        statusText.textContent = message;
    }

    // 根据状态类型更新样式
    if (statusBar) {
        // 重置默认样式
        statusBar.style.background = 'rgba(6, 182, 212, 0.1)';
        statusBar.style.borderColor = 'rgba(6, 182, 212, 0.2)';
        statusBar.style.color = 'var(--accent)';

        switch (type) {
            case 'success':
                statusBar.style.background = 'rgba(34, 197, 94, 0.1)';
                statusBar.style.borderColor = 'rgba(34, 197, 94, 0.2)';
                statusBar.style.color = '#86efac';
                break;
            case 'scanned':
                statusBar.style.background = 'rgba(251, 146, 60, 0.1)';
                statusBar.style.borderColor = 'rgba(251, 146, 60, 0.2)';
                statusBar.style.color = '#fdba74';
                break;
            case 'error':
            case 'cancelled':
            case 'timeout':
                statusBar.style.background = 'rgba(239, 68, 68, 0.1)';
                statusBar.style.borderColor = 'rgba(239, 68, 68, 0.2)';
                statusBar.style.color = '#fca5a5';
                break;
            case 'info':
            case 'waiting':
            default:
                // 使用默认样式
                break;
        }
    }
}

// 显示错误信息
function showError(message) {
    const errorDiv = document.getElementById('login-error');
    if (errorDiv) {
        errorDiv.textContent = message;
        errorDiv.style.display = 'block';

        // 3秒后自动隐藏
        setTimeout(() => {
            hideError();
        }, 3000);
    }
}

// 隐藏错误信息
function hideError() {
    const errorDiv = document.getElementById('login-error');
    if (errorDiv) {
        errorDiv.style.display = 'none';
    }
}

// 页面卸载时清理轮询
window.addEventListener('beforeunload', function () {
    stopPolling();
});
