// 获取存储的token
function getToken() {
    return localStorage.getItem('token');
}

// 设置token
function setToken(token) {
    localStorage.setItem('token', token);
    // 同时保存到Cookie，供服务端验证使用
    document.cookie = `token=${token}; path=/; max-age=86400`; // 24小时有效期
}

// 清除token
function clearToken() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    // 同时清除Cookie
    document.cookie = 'token=; path=/; max-age=0';
}

// 全局错误处理函数
async function handleUnauthorized(response) {
    if (response.status === 401) {
        clearToken();
        alert('登录已过期，请重新登录');
        window.location.href = '/Home/Index';
        return true;
    }
    return false;
}

// 登录
$('#login-form').on('submit', async function (e) {
    e.preventDefault();

    const username = $('#username').val().trim();
    const password = $('#password').val().trim();

    if (!username || !password) {
        showError('用户名和密码不能为空');
        return;
    }

    $('#login-btn').prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> 登录中...');

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
        body: JSON.stringify({ username, password })
        });

        const result = await response.json();

        if (result.success) {
            setToken(result.token);
            localStorage.setItem('user', JSON.stringify(result.user));
            // 密码登录成功后也跳转到绘图页面
            window.location.href = '/Home/Drawing';
        } else {
            showError(result.message || '登录失败', 'login');
        }
    } catch (error) {
        showError('登录失败: ' + error.message, 'login');
    } finally {
        $('#login-btn').prop('disabled', false).html('<i class="fas fa-sign-in-alt"></i> 登录');
    }
});

// 显示错误信息
function showError(message, prefix = '') {
    const errorId = prefix ? `#${prefix}-error` : '#login-error';
    $(errorId).text(message).fadeIn();
    setTimeout(() => {
        $(errorId).fadeOut();
    }, 3000);
}

// 显示成功信息
function showSuccess(message, prefix = '') {
    const successId = prefix ? `#${prefix}-success` : `#${prefix}-success`;
    $(successId).text(message).fadeIn();
    setTimeout(() => {
        $(successId).fadeOut();
    }, 3000);
}

// 登出
async function logout() {
    try {
        // 调用后端登出接口，清除服务端的用户令牌
        await fetch('/api/auth/logout', {
            method: 'POST',
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });
    } catch (error) {

    } finally {
        // 清除本地存储的令牌和用户信息
        clearToken();
        window.location.href = '/Home/Index';
    }
}

// 检查登录状态（保留函数但不使用，以防其他地方调用）
function checkAuth() {
    // 服务端验证已启用，此函数不再使用
    // 但保留以防其他代码调用
    const token = getToken();
    if (!token) {
        window.location.href = '/Home/Index';
        return false;
    }
    return true;
}

// 检查管理员权限
function checkAdmin() {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    if (!user.isAdmin) {
        alert('需要管理员权限');
        window.location.href = '/Home/Index';
        return false;
    }
    return true;
}

// 检查管理员权限并跳转到管理页面
function goToAdmin() {
    if (!checkAuth() || !checkAdmin()) return;
    window.location.href = '/Home/Admin';
}

// 标签页切换功能
$(document).ready(function() {




    
    // 标签页切换 - 使用更具体的选择器
    $('.login-container .tab-btn').on('click', function() {

        const targetTab = $(this).data('tab');

        
        // 切换按钮状态
        $('.login-container .tab-btn').removeClass('active');
        $(this).addClass('active');
        
        // 切换面板显示
        $('.login-container .tab-panel').removeClass('active');
        $(`#${targetTab}-panel`).addClass('active');
        
        // 清除错误和成功信息
        $('.login-container .alert').hide();
    });

    // 发送验证码 - 使用更具体的选择器
    $('.login-container #send-code-btn').on('click', async function() {

        const email = $('.login-container #reg-email').val().trim();
        
        if (!email) {
            showError('请输入邮箱地址', 'register');
            return;
        }
        
        if (!isValidEmail(email)) {
            showError('邮箱格式不正确', 'register');
            return;
        }

        const $btn = $(this);
        const originalText = $btn.html();
        
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> 发送中...');

        try {
            const response = await fetch('/api/auth/send-verification-code', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ email })
            });

            const result = await response.json();

            if (result.success) {
                showSuccess(result.message, 'register');
                startCountdown($btn, 60); // 60秒倒计时
            } else {
                showError(result.message, 'register');
            }
        } catch (error) {
            showError('发送验证码失败: ' + error.message, 'register');
        } finally {
            if (!$btn.hasClass('countdown')) {
                $btn.prop('disabled', false).html(originalText);
            }
        }
    });

    // 注册表单提交
    $('#register-form').on('submit', async function(e) {

        e.preventDefault();

        const username = $('.login-container #reg-username').val().trim();
        const email = $('.login-container #reg-email').val().trim();
        const verificationCode = $('.login-container #reg-verification-code').val().trim();
        const password = $('.login-container #reg-password').val().trim();
        const confirmPassword = $('.login-container #reg-confirm-password').val().trim();

        // 表单验证
        if (!username || !email || !verificationCode || !password || !confirmPassword) {
            showError('请填写所有必填字段', 'register');
            return;
        }

        if (username.length < 3 || username.length > 50) {
            showError('用户名长度必须在3-50个字符之间', 'register');
            return;
        }

        if (!isValidEmail(email)) {
            showError('邮箱格式不正确', 'register');
            return;
        }

        if (verificationCode.length !== 6) {
            showError('验证码必须是6位数字', 'register');
            return;
        }

        if (password.length < 6 || password.length > 100) {
            showError('密码长度必须在6-100个字符之间', 'register');
            return;
        }

        if (password !== confirmPassword) {
            showError('两次输入的密码不一致', 'register');
            return;
        }

        $('#register-btn').prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> 注册中...');

        try {
            const response = await fetch('/api/auth/register', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    username,
                    email,
                    password,
                    confirmPassword,
                    verificationCode
                })
            });

            const result = await response.json();

            if (result.success) {
                setToken(result.token);
                localStorage.setItem('user', JSON.stringify(result.user));
                // 注册成功后直接跳转到绘图页面
                window.location.href = '/Home/Drawing';
            } else {
                showError(result.message || '注册失败', 'register');
            }
        } catch (error) {
            showError('注册失败: ' + error.message, 'register');
        } finally {
            $('#register-btn').prop('disabled', false).html('<i class="fas fa-user-plus"></i> 注册');
        }
    });
});

// 邮箱验证函数
function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

// 倒计时函数
function startCountdown($btn, seconds) {
    $btn.addClass('countdown').prop('disabled', true);
    
    const interval = setInterval(() => {
        seconds--;
        $btn.html(`重新发送(${seconds}s)`);
        
        if (seconds <= 0) {
            clearInterval(interval);
            $btn.removeClass('countdown').prop('disabled', false).html('<i class="fas fa-paper-plane"></i> 发送验证码');
        }
    }, 1000);
}