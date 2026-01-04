// 管理员登录功能
$(document).ready(function () {
    $('#admin-login-form').on('submit', async function (e) {
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
                // 检查是否为管理员
                if (result.user && result.user.isAdmin) {
                    // 保存token和用户信息
                    localStorage.setItem('token', result.token);
                    localStorage.setItem('user', JSON.stringify(result.user));
                    // 跳转到管理后台主页
                    window.location.href = '/Admin/Index';
                } else {
                    showError('需要管理员权限才能访问后台管理系统');
                }
            } else {
                showError(result.message || '登录失败');
            }
        } catch (error) {
            showError('登录失败: ' + error.message);
        } finally {
            $('#login-btn').prop('disabled', false).html('<i class="fas fa-sign-in-alt"></i> 登录');
        }
    });
});

// 显示错误信息
function showError(message) {
    $('#error-message').text(message).fadeIn();
    setTimeout(() => {
        $('#error-message').fadeOut();
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
        window.location.href = '/Admin/Login';
    }
}

// 获取存储的token
function getToken() {
    return localStorage.getItem('token');
}

// 设置token
function setToken(token) {
    localStorage.setItem('token', token);
}

// 清除token
function clearToken() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    // 同时清除Cookie
    document.cookie = 'token=; path=/; max-age=0';
}