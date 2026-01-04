// 全局变量
let currentPage = 1;
let pageSize = 20;

// 获取存储的token
function getToken() {
    return localStorage.getItem('token');
}

// 检查登录状态
function isAuthenticated() {
    const token = getToken();
    if (!token) {
        window.location.href = '/Home/Index';
        return false;
    }
    return true;
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
        console.error('登出失败:', error);
    } finally {
        // 清除本地存储的令牌和用户信息
        localStorage.removeItem('token');
        localStorage.removeItem('username');
        localStorage.removeItem('user');
        window.location.href = '/Home/Index';
    }
}

// 显示消息提示
function showToast(message, type = 'info') {
    // 创建toast元素
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
        <i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'error' ? 'fa-exclamation-circle' : type === 'warning' ? 'fa-exclamation-triangle' : 'fa-info-circle'}"></i>
        <span>${message}</span>
    `;
    
    // 添加样式
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        padding: 12px 24px;
        background: white;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        display: flex;
        align-items: center;
        gap: 10px;
        z-index: 9999;
        animation: slideIn 0.3s ease-out;
        border-left: 4px solid ${type === 'success' ? '#10b981' : type === 'error' ? '#ef4444' : type === 'warning' ? '#f59e0b' : '#6366f1'};
    `;
    
    // 添加到页面
    document.body.appendChild(toast);
    
    // 自动移除
    setTimeout(() => {
        toast.style.animation = 'slideOut 0.3s ease-out';
        setTimeout(() => {
            document.body.removeChild(toast);
        }, 300);
    }, 3000);
}

// 页面加载完成后初始化
document.addEventListener('DOMContentLoaded', function() {
    // 检查认证状态
    if (!isAuthenticated()) {
        window.location.href = '/Home/Index';
        return;
    }
    
    // 初始化页面
    initProfile();
});

// 初始化用户信息页面
async function initProfile() {
    try {
        // 显示用户名
        const username = localStorage.getItem('username') || '用户';
        const usernameDisplay = document.getElementById('username-display');
        if (usernameDisplay) {
            usernameDisplay.querySelector('.username-text').textContent = username;
        }

        // 加载用户基本信息
        await loadUserInfo();
        
        // 加载签到状态
        await loadCheckInStatus();
        
        // 加载积分历史
        await loadPointsHistory();
        
        // 加载充值记录
        await loadRechargeRecords();

        // 绑定密码修改表单事件
        bindPasswordForm();

    } catch (error) {
        console.error('初始化用户信息页面失败:', error);
        showToast('初始化页面失败', 'error');
    }
}

// 加载用户基本信息
async function loadUserInfo() {
    try {
        const token = localStorage.getItem('token');
        if (!token) {
            showToast('请先登录', 'warning');
            window.location.href = '/Home/Index';
            return;
        }

        const response = await fetch('/api/userprofile/info', {
            method: 'GET',
            headers: {
                'Authorization': 'Bearer ' + token,
                'Content-Type': 'application/json'
            }
        });

        if (response.status === 401) {
            showToast('登录已过期，请重新登录', 'warning');
            localStorage.removeItem('token');
            localStorage.removeItem('username');
            window.location.href = '/Home/Index';
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            const userInfo = result.data;
            
            // 更新页面显示
            const usernameEl = document.getElementById('profile-username');
            const emailEl = document.getElementById('profile-email');
            const pointsEl = document.getElementById('profile-points');
            const createdEl = document.getElementById('profile-created');
            
            if (usernameEl) usernameEl.textContent = userInfo.username || '未知';
            if (emailEl) emailEl.textContent = userInfo.email || '未设置';
            if (pointsEl) pointsEl.textContent = userInfo.points || 0;
            
            // 格式化注册时间
            if (createdEl) {
                if (userInfo.createdAt) {
                    const createdDate = new Date(userInfo.createdAt);
                    createdEl.textContent = createdDate.toLocaleDateString('zh-CN');
                } else {
                    createdEl.textContent = '未知';
                }
            }
        } else {
            showToast(result.message || '加载用户信息失败', 'error');
        }
    } catch (error) {
        console.error('加载用户信息失败:', error);
        showToast('加载用户信息失败', 'error');
    }
}

// 加载签到状态
async function loadCheckInStatus() {
    try {
        const token = localStorage.getItem('token');
        if (!token) {
            return;
        }

        const response = await fetch('/api/userprofile/check-in-status', {
            method: 'GET',
            headers: {
                'Authorization': 'Bearer ' + token,
                'Content-Type': 'application/json'
            }
        });

        if (response.status === 401) {
            localStorage.removeItem('token');
            localStorage.removeItem('username');
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            const data = result.data;
            
            // 更新每日积分
            const dailyPointsEl = document.getElementById('check-in-daily-points');
            if (dailyPointsEl) {
                dailyPointsEl.textContent = data.dailyPoints || 10;
            }
            
            // 更新连续签到天数
            const consecutiveDaysEl = document.getElementById('check-in-consecutive-days');
            if (consecutiveDaysEl) {
                consecutiveDaysEl.textContent = data.consecutiveDays || 0;
            }
            
            // 更新上次签到日期
            const lastDateEl = document.getElementById('check-in-last-date');
            if (lastDateEl) {
                if (data.lastCheckInDate) {
                    const lastDate = new Date(data.lastCheckInDate);
                    lastDateEl.textContent = lastDate.toLocaleDateString('zh-CN');
                } else {
                    lastDateEl.textContent = '从未签到';
                }
            }
            
            // 更新签到按钮状态
            const checkInBtn = document.getElementById('btn-check-in');
            if (checkInBtn) {
                if (data.hasCheckedIn) {
                    checkInBtn.disabled = true;
                    checkInBtn.innerHTML = '<i class="fas fa-check"></i><span>今日已签到</span>';
                } else {
                    checkInBtn.disabled = false;
                    checkInBtn.innerHTML = '<i class="fas fa-hand-pointer"></i><span>立即签到</span>';
                }
            }
        }
    } catch (error) {
        console.error('加载签到状态失败:', error);
    }
}

// 签到
async function checkIn() {
    try {
        const token = localStorage.getItem('token');
        if (!token) {
            showToast('请先登录', 'warning');
            window.location.href = '/Home/Index';
            return;
        }

        const checkInBtn = document.getElementById('btn-check-in');
        if (checkInBtn) {
            checkInBtn.disabled = true;
            checkInBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i><span>签到中...</span>';
        }

        const response = await fetch('/api/userprofile/check-in', {
            method: 'POST',
            headers: {
                'Authorization': 'Bearer ' + token,
                'Content-Type': 'application/json'
            }
        });

        if (response.status === 401) {
            showToast('登录已过期，请重新登录', 'warning');
            localStorage.removeItem('token');
            localStorage.removeItem('username');
            setTimeout(() => {
                window.location.href = '/Home/Index';
            }, 1000);
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            showToast(result.message, 'success');
            
            // 更新用户积分
            const pointsEl = document.getElementById('profile-points');
            if (pointsEl && result.data && result.data.currentPoints !== undefined) {
                pointsEl.textContent = result.data.currentPoints;
            }
            
            // 更新连续签到天数
            const consecutiveDaysEl = document.getElementById('check-in-consecutive-days');
            if (consecutiveDaysEl && result.data && result.data.consecutiveDays !== undefined) {
                consecutiveDaysEl.textContent = result.data.consecutiveDays;
            }
            
            // 更新上次签到日期
            const lastDateEl = document.getElementById('check-in-last-date');
            if (lastDateEl) {
                const today = new Date();
                lastDateEl.textContent = today.toLocaleDateString('zh-CN');
            }
            
            // 更新签到按钮状态
            if (checkInBtn) {
                checkInBtn.innerHTML = '<i class="fas fa-check"></i><span>今日已签到</span>';
            }
            
            // 刷新积分历史
            await loadPointsHistory();
        } else {
            showToast(result.message || '签到失败', 'error');
            if (checkInBtn) {
                checkInBtn.disabled = false;
                checkInBtn.innerHTML = '<i class="fas fa-hand-pointer"></i><span>立即签到</span>';
            }
        }
    } catch (error) {
        console.error('签到失败:', error);
        showToast('签到失败，请稍后重试', 'error');
        const checkInBtn = document.getElementById('btn-check-in');
        if (checkInBtn) {
            checkInBtn.disabled = false;
            checkInBtn.innerHTML = '<i class="fas fa-hand-pointer"></i><span>立即签到</span>';
        }
    }
}

// 加载积分历史
async function loadPointsHistory(page = 1) {
    try {
        const container = document.getElementById('points-history-container');
        if (!container) return;
        
        container.innerHTML = '<div class="loading"><div class="spinner"></div><p>加载中...</p></div>';

        const token = localStorage.getItem('token');
        if (!token) {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-chart-line"></i><p>请先登录</p></div>';
            return;
        }

        const response = await fetch(`/api/userprofile/points-history?page=${page}&pageSize=${pageSize}`, {
            method: 'GET',
            headers: {
                'Authorization': 'Bearer ' + token,
                'Content-Type': 'application/json'
            }
        });

        if (response.status === 401) {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-chart-line"></i><p>登录已过期</p></div>';
            localStorage.removeItem('token');
            localStorage.removeItem('username');
            setTimeout(() => {
                window.location.href = '/Home/Index';
            }, 1000);
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            renderPointsHistory(result.data, result.pagination);
        } else {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-chart-line"></i><p>加载积分历史失败</p></div>';
            showToast(result.message || '加载积分历史失败', 'error');
        }
    } catch (error) {
        console.error('加载积分历史失败:', error);
        const container = document.getElementById('points-history-container');
        if (container) {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-chart-line"></i><p>加载积分历史失败</p></div>';
        }
        showToast('加载积分历史失败', 'error');
    }
}

// 渲染积分历史列表
function renderPointsHistory(historyData, pagination) {
    const container = document.getElementById('points-history-container');
    
    if (!historyData || historyData.length === 0) {
        container.innerHTML = '<div class="empty-state"><i class="fas fa-chart-line"></i><p>暂无积分变动记录</p></div>';
        document.getElementById('points-pagination').style.display = 'none';
        return;
    }

    const historyHtml = historyData.map(item => `
        <div class="points-history-item">
            <div class="points-info">
                <div class="points-amount ${item.type}">
                    <i class="fas fa-gem"></i>
                    <span>${item.points > 0 ? '+' : ''}${item.points}</span>
                </div>
                <div class="points-description">${item.description}</div>
            </div>
            <div class="points-date">
                ${formatDate(item.createdAt)}
            </div>
        </div>
    `).join('');

    container.innerHTML = `<div class="points-history-list">${historyHtml}</div>`;

    // 渲染分页
    if (pagination && pagination.totalPages > 1) {
        renderPagination('points-pagination-nav', pagination, loadPointsHistory);
        document.getElementById('points-pagination').style.display = 'block';
    } else {
        document.getElementById('points-pagination').style.display = 'none';
    }
}

// 加载充值记录
async function loadRechargeRecords(page = 1) {
    try {
        const container = document.getElementById('recharge-records-container');
        if (!container) return;
        
        container.innerHTML = '<div class="loading"><div class="spinner"></div><p>加载中...</p></div>';

        const token = localStorage.getItem('token');
        if (!token) {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-receipt"></i><p>请先登录</p></div>';
            return;
        }

        const response = await fetch(`/api/userprofile/recharge-records?page=${page}&pageSize=${pageSize}`, {
            method: 'GET',
            headers: {
                'Authorization': 'Bearer ' + token,
                'Content-Type': 'application/json'
            }
        });

        if (response.status === 401) {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-receipt"></i><p>登录已过期</p></div>';
            localStorage.removeItem('token');
            localStorage.removeItem('username');
            setTimeout(() => {
                window.location.href = '/Home/Index';
            }, 1000);
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            renderRechargeRecords(result.data, result.pagination);
        } else {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-receipt"></i><p>加载充值记录失败</p></div>';
            showToast(result.message || '加载充值记录失败', 'error');
        }
    } catch (error) {
        console.error('加载充值记录失败:', error);
        const container = document.getElementById('recharge-records-container');
        if (container) {
            container.innerHTML = '<div class="empty-state"><i class="fas fa-receipt"></i><p>加载充值记录失败</p></div>';
        }
        showToast('加载充值记录失败', 'error');
    }
}

// 渲染充值记录列表
function renderRechargeRecords(recordsData, pagination) {
    const container = document.getElementById('recharge-records-container');
    
    if (!recordsData || recordsData.length === 0) {
        container.innerHTML = '<div class="empty-state"><i class="fas fa-receipt"></i><p>暂无充值记录</p></div>';
        document.getElementById('recharge-pagination').style.display = 'none';
        return;
    }

    const recordsHtml = recordsData.map(record => `
        <div class="recharge-record-item">
            <div class="recharge-header">
                <div class="recharge-order">订单号: ${record.orderNo}</div>
                <div class="recharge-status ${record.orderStatus.toLowerCase()}">${record.statusText}</div>
            </div>
            <div class="recharge-details">
                <div class="recharge-amount">¥${record.amount}</div>
                <div class="recharge-points">+${record.points} 积分</div>
                <div class="recharge-date">${formatDate(record.createdAt)}</div>
            </div>
        </div>
    `).join('');

    container.innerHTML = `<div class="recharge-records-list">${recordsHtml}</div>`;

    // 渲染分页
    if (pagination && pagination.totalPages > 1) {
        renderPagination('recharge-pagination-nav', pagination, loadRechargeRecords);
        document.getElementById('recharge-pagination').style.display = 'block';
    } else {
        document.getElementById('recharge-pagination').style.display = 'none';
    }
}

// 渲染分页组件
function renderPagination(containerId, pagination, loadFunction) {
    const container = document.getElementById(containerId);
    if (!container) return;

    let paginationHtml = '';

    // 上一页按钮
    paginationHtml += `<button class="page-btn" ${pagination.page <= 1 ? 'disabled' : ''} 
                       onclick="${loadFunction.name}(${pagination.page - 1})">
                       <i class="fas fa-chevron-left"></i></button>`;

    // 页码按钮
    const startPage = Math.max(1, pagination.page - 2);
    const endPage = Math.min(pagination.totalPages, pagination.page + 2);

    if (startPage > 1) {
        paginationHtml += `<button class="page-btn" onclick="${loadFunction.name}(1)">1</button>`;
        if (startPage > 2) {
            paginationHtml += '<span style="padding: 0 8px;">...</span>';
        }
    }

    for (let i = startPage; i <= endPage; i++) {
        paginationHtml += `<button class="page-btn ${i === pagination.page ? 'active' : ''}" 
                           onclick="${loadFunction.name}(${i})">${i}</button>`;
    }

    if (endPage < pagination.totalPages) {
        if (endPage < pagination.totalPages - 1) {
            paginationHtml += '<span style="padding: 0 8px;">...</span>';
        }
        paginationHtml += `<button class="page-btn" onclick="${loadFunction.name}(${pagination.totalPages})">
                           ${pagination.totalPages}</button>`;
    }

    // 下一页按钮
    paginationHtml += `<button class="page-btn" ${pagination.page >= pagination.totalPages ? 'disabled' : ''} 
                       onclick="${loadFunction.name}(${pagination.page + 1})">
                       <i class="fas fa-chevron-right"></i></button>`;

    container.innerHTML = paginationHtml;
}

// 绑定密码修改表单事件
function bindPasswordForm() {
    const form = document.getElementById('change-password-form');
    if (!form) return;

    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const oldPassword = document.getElementById('old-password').value;
        const newPassword = document.getElementById('new-password').value;
        const confirmPassword = document.getElementById('confirm-password').value;

        // 表单验证
        if (!oldPassword || !newPassword || !confirmPassword) {
            showToast('请填写所有密码字段', 'warning');
            return;
        }

        if (newPassword !== confirmPassword) {
            showToast('新密码和确认密码不匹配', 'warning');
            return;
        }

        if (newPassword.length < 6) {
            showToast('新密码长度至少为6位', 'warning');
            return;
        }

        // 提交密码修改请求
        await changePassword(oldPassword, newPassword);
    });
}

// 修改密码
async function changePassword(oldPassword, newPassword) {
    try {
        const token = localStorage.getItem('token');
        if (!token) {
            showToast('请先登录', 'warning');
            window.location.href = '/Home/Index';
            return;
        }

        const response = await fetch('/api/userprofile/change-password', {
            method: 'PUT',
            headers: {
                'Authorization': 'Bearer ' + token,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                oldPassword: oldPassword,
                newPassword: newPassword
            })
        });

        if (response.status === 401) {
            showToast('登录已过期，请重新登录', 'warning');
            localStorage.removeItem('token');
            localStorage.removeItem('username');
            setTimeout(() => {
                window.location.href = '/Home/Index';
            }, 1000);
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            showToast('密码修改成功', 'success');
            // 清空表单
            const form = document.getElementById('change-password-form');
            if (form) form.reset();
        } else {
            showToast(result.message || '密码修改失败', 'error');
        }
    } catch (error) {
        console.error('修改密码失败:', error);
        showToast('修改密码失败', 'error');
    }
}

// 格式化日期
function formatDate(dateString) {
    if (!dateString) return '未知';
    
    const date = new Date(dateString);
    const now = new Date();
    const diff = now - date;
    
    // 如果是今天
    if (diff < 24 * 60 * 60 * 1000) {
        return '今天 ' + date.toLocaleTimeString('zh-CN', { 
            hour: '2-digit', 
            minute: '2-digit' 
        });
    }
    
    // 如果是昨天
    if (diff < 48 * 60 * 60 * 1000) {
        return '昨天 ' + date.toLocaleTimeString('zh-CN', { 
            hour: '2-digit', 
            minute: '2-digit' 
        });
    }
    
    // 其他日期
    return date.toLocaleDateString('zh-CN') + ' ' + 
           date.toLocaleTimeString('zh-CN', { 
               hour: '2-digit', 
               minute: '2-digit' 
           });
}

// 跳转到绘图页面
function switchToDrawing() {
    window.location.href = '/Home/Drawing';
}