// 后台管理功能
let currentModels = [];
let currentUsers = [];

$(document).ready(function () {
    // 检查登录状态
    const token = localStorage.getItem('token');
    const userStr = localStorage.getItem('user');
    
    if (!token || !userStr) {

        window.location.href = '/Admin/Login';
        return;
    }
    
    let user;
    try {
        user = JSON.parse(userStr);
    } catch (e) {

        localStorage.removeItem('token');
        localStorage.removeItem('user');
        window.location.href = '/Admin/Login';
        return;
    }
    
    if (!user.isAdmin) {

        localStorage.removeItem('token');
        localStorage.removeItem('user');
        window.location.href = '/Admin/Login';
        return;
    }
    
    $('#username-display').text(user.username || '管理员');
    
    // 初始化各模块
    loadDashboard();
    loadModels();
    loadUsers();
});

// 切换标签页
function switchTab(tabName) {
    // 隐藏所有tab内容
    $('.tab-content').removeClass('active');
    // 移除所有tab按钮的active类
    $('.nav-tab').removeClass('active');
    
    // 显示选中的tab内容
    $('#' + tabName).addClass('active');
    // 为选中的tab按钮添加active类
    event.currentTarget.classList.add('active');
}

// 加载仪表盘数据
async function loadDashboard() {
    // 这里可以从API获取统计数据
    $('#total-users').text('128');
    $('#total-drawings').text('342');
    $('#active-models').text('5');
    $('#total-points').text('12,860');
}

// 加载模型列表
async function loadModels() {
    try {
        showLoading('#models-table-body');
        
        const response = await fetch('/admin/api/admin/models', {
            headers: {
                'Authorization': 'Bearer ' + localStorage.getItem('token')
            }
        });
        
        if (response.status === 401 || response.status === 403) {
            window.location.href = '/Admin/Login';
            return;
        }
        
        const result = await response.json();
        
        if (result.success) {
            currentModels = result.data;
            renderModels(currentModels);
        } else {
            showError('#models-table-body', '加载模型失败: ' + result.message);
        }
    } catch (error) {

        showError('#models-table-body', '加载模型失败');
    }
}

// 渲染模型列表
function renderModels(models) {
    const tbody = $('#models-table-body');
    tbody.empty();
    
    if (models.length === 0) {
        tbody.html('<tr><td colspan="6" style="text-align: center;">暂无模型数据</td></tr>');
        return;
    }
    
    models.forEach(model => {
        const row = `
            <tr>
                <td>${model.id}</td>
                <td>${model.modelName}</td>
                <td>${model.description || '-'}</td>
                <td>${model.pointCost}</td>
                <td>${model.isActive ? '<span class="badge badge-success">启用</span>' : '<span class="badge badge-danger">禁用</span>'}</td>
                <td>
                    <div class="btn-group">
                        <button class="btn btn-sm btn-secondary" onclick="editModel(${model.id})">
                            <i class="fas fa-edit"></i> 编辑
                        </button>
                        <button class="btn btn-sm btn-danger" onclick="deleteModel(${model.id})">
                            <i class="fas fa-trash"></i> 删除
                        </button>
                    </div>
                </td>
            </tr>
        `;
        tbody.append(row);
    });
}

// 加载用户列表
async function loadUsers() {
    try {
        showLoading('#users-table-body');
        
        const response = await fetch('/admin/api/admin/users', {
            headers: {
                'Authorization': 'Bearer ' + localStorage.getItem('token')
            }
        });
        
        if (response.status === 401 || response.status === 403) {
            window.location.href = '/Admin/Login';
            return;
        }
        
        const result = await response.json();
        
        if (result.success) {
            currentUsers = result.data;
            renderUsers(currentUsers);
        } else {
            showError('#users-table-body', '加载用户失败: ' + result.message);
        }
    } catch (error) {

        showError('#users-table-body', '加载用户失败');
    }
}

// 渲染用户列表
function renderUsers(users) {
    const tbody = $('#users-table-body');
    tbody.empty();
    
    if (users.length === 0) {
        tbody.html('<tr><td colspan="7" style="text-align: center;">暂无用户数据</td></tr>');
        return;
    }
    
    users.forEach(user => {
        const row = `
            <tr>
                <td>${user.id}</td>
                <td>${user.username}</td>
                <td>${user.email || '-'}</td>
                <td>${user.points}</td>
                <td>${user.isAdmin ? '<span class="badge badge-warning">管理员</span>' : '<span class="badge badge-info">普通用户</span>'}</td>
                <td>${user.isActive ? '<span class="badge badge-success">正常</span>' : '<span class="badge badge-danger">禁用</span>'}</td>
                <td>
                    <div class="btn-group">
                        <button class="btn btn-sm btn-secondary" onclick="editUser(${user.id})">
                            <i class="fas fa-edit"></i> 编辑
                        </button>
                    </div>
                </td>
            </tr>
        `;
        tbody.append(row);
    });
}

// 模型操作函数
function openModelModal(model = null) {
    alert('模型编辑功能占位符');
}

function editModel(id) {
    alert('编辑模型功能占位符，ID: ' + id);
}

function deleteModel(id) {
    if (confirm('确定要删除这个模型吗？')) {
        alert('删除模型功能占位符，ID: ' + id);
    }
}

// 用户操作函数
function editUser(id) {
    alert('编辑用户功能占位符，ID: ' + id);
}

// 辅助函数
function showLoading(selector) {
    $(selector).html('<tr><td colspan="10" style="text-align: center;">加载中...</td></tr>');
}

function showError(selector, message) {
    $(selector).html(`<tr><td colspan="10" style="text-align: center; color: var(--danger-color);">${message}</td></tr>`);
}

function logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/Admin/Login';
}