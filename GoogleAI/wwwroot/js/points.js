// 全局变量
let currentPackage = null;
let currentPage = 1;
let pageSize = 10;
let totalOrders = 0;

// 页面加载完成后初始化
$(document).ready(function () {
    loadUserInfo();
    loadPackages();
    loadPaymentOrders();
});

// 加载用户信息
async function loadUserInfo() {
    try {
        const response = await fetch('/api/userprofile/info', {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        if (response.status === 401) {
            handleUnauthorized(response);
            return;
        }

        const result = await response.json();
        
        if (result.success) {
            $('#username-display .username-text').text(result.data.username);
            $('#points-display .points-text').text(result.data.points + ' 积分');
            $('#user-points-balance').text(result.data.points);
        }
    } catch (error) {
        console.error('加载用户信息失败:', error);
    }
}

// 加载积分套餐
async function loadPackages() {
    try {
        const response = await fetch('/api/pointpurchase/packages', {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        if (response.status === 401) {
            handleUnauthorized(response);
            return;
        }

        const result = await response.json();

        if (result.success && result.data.length > 0) {
            const packagesHtml = result.data.map(pkg => `
                <div class="package-card ${pkg.points >= 500 && pkg.points < 1000 ? 'recommended' : ''}" data-package-id="${pkg.id}">
                    ${pkg.points >= 500 && pkg.points < 1000 ? '<div class="package-badge">推荐</div>' : ''}
                    <div class="package-icon">
                        <i class="fas fa-gem"></i>
                    </div>
                    <div class="package-name">${pkg.name}</div>
                    <div class="package-points">
                        <span class="points-value">${pkg.points}</span>
                        <span style="font-size: 0.875rem; color: var(--text-secondary);">积分</span>
                    </div>
                    <div class="package-price">
                        <span class="price-symbol">¥</span>
                        <span class="price-value">${pkg.price.toFixed(2)}</span>
                    </div>
                    ${pkg.description ? `<div class="package-description">${pkg.description}</div>` : ''}
                    <button class="btn btn-primary package-purchase-btn" onclick="selectPackage(${pkg.id})">
                        <i class="fas fa-shopping-cart"></i> 立即购买
                    </button>
                </div>
            `).join('');

            $('#packages-container').html(packagesHtml);
        } else {
            $('#packages-container').html(`
                <div class="empty-state" style="grid-column: 1 / -1;">
                    <i class="fas fa-box-open"></i>
                    <p>暂无可用套餐</p>
                </div>
            `);
        }
    } catch (error) {
        console.error('加载套餐失败:', error);
        $('#packages-container').html(`
            <div class="empty-state" style="grid-column: 1 / -1;">
                <i class="fas fa-exclamation-circle"></i>
                <p>加载失败，请刷新重试</p>
            </div>
        `);
    }
}

// 选择套餐
function selectPackage(packageId) {
    const packages = document.querySelectorAll('.package-card');
    const selectedPackage = Array.from(packages).find(pkg => pkg.dataset.packageId == packageId);
    
    if (selectedPackage) {
        const name = selectedPackage.querySelector('.package-name').textContent;
        const points = parseInt(selectedPackage.querySelector('.points-value').textContent);
        const price = parseFloat(selectedPackage.querySelector('.price-value').textContent);
        
        currentPackage = {
            id: packageId,
            name: name,
            points: points,
            price: price
        };
        
        showPaymentModal();
    }
}

// 显示支付弹窗
async function showPaymentModal() {
    if (!currentPackage) {
        alert('请先选择套餐');
        return;
    }

    $('#payment-amount').text(`¥${currentPackage.price.toFixed(2)}`);
    $('#payment-points').text(`${currentPackage.points} 积分`);
    $('#payment-package').text(currentPackage.name);
    
    $('#payment-qr-code').html(`
        <div class="loading">
            <div class="spinner"></div>
            <p>创建订单中...</p>
        </div>
    `);
    
    //$('#paymentModal').fadeIn();
    $('#paymentModal').css('display', 'flex').hide().fadeIn();

    try {
        // 创建支付订单
        const response = await fetch('/api/pointpurchase/create-order', {
            method: 'POST',
            headers: {
                'Authorization': 'Bearer ' + getToken(),
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                packageId: currentPackage.id
            })
        });

        if (response.status === 401) {
            handleUnauthorized(response);
            closePaymentModal();
            return;
        }

        const result = await response.json();

        if (result.success) {
            console.log('订单创建成功，数据：', result.data);
            console.log('CodeUrl:', result.data.codeUrl);
            
            // 生成支付二维码 (Native支付)
            $('#payment-qr-code').html(`
                <div class="qr-container">
                    <div id="qrcode"></div>
                    <p style="margin-top: var(--spacing-md); font-weight: 600;">微信扫码支付</p>
                    <p style="font-size: 0.875rem; color: var(--text-secondary);">请使用微信扫描上方二维码完成支付</p>
                </div>
            `);

            // 使用qrcode.js生成二维码
            const qrCodeElement = document.getElementById('qrcode');
            if (qrCodeElement && result.data.codeUrl) {
                console.log('开始生成二维码，QRCode对象是否存在：', typeof QRCode);
                
                if (typeof QRCode !== 'undefined') {
                    try {
                        new QRCode(qrCodeElement, {
                            text: result.data.codeUrl,
                            width: 200,
                            height: 200,
                            colorDark: "#000000",
                            colorLight: "#ffffff",
                            correctLevel: QRCode.CorrectLevel.H
                        });
                        console.log('二维码生成成功');
                    } catch (error) {
                        console.error('二维码生成失败：', error);
                        $('#payment-qr-code').html(`
                            <div class="error-message">
                                <i class="fas fa-exclamation-triangle"></i>
                                <p>二维码生成失败：${error.message}</p>
                                <p style="font-size: 0.75rem; color: var(--text-muted); margin-top: 10px;">
                                    CodeUrl: ${result.data.codeUrl.substring(0, 50)}...
                                </p>
                            </div>
                        `);
                    }
                } else {
                    console.error('QRCode库未加载');
                    $('#payment-qr-code').html(`
                        <div class="error-message">
                            <i class="fas fa-exclamation-triangle"></i>
                            <p>二维码库未正确加载</p>
                            <p style="font-size: 0.75rem; color: var(--text-muted); margin-top: 10px;">
                                请刷新页面重试
                            </p>
                        </div>
                    `);
                }
            } else {
                console.error('二维码元素不存在或codeUrl为空');
                if (!qrCodeElement) {
                    $('#payment-qr-code').html(`
                        <div class="error-message">
                            <i class="fas fa-exclamation-triangle"></i>
                            <p>二维码容器不存在</p>
                        </div>
                    `);
                } else if (!result.data.codeUrl) {
                    $('#payment-qr-code').html(`
                        <div class="error-message">
                            <i class="fas fa-exclamation-triangle"></i>
                            <p>未获取到支付链接</p>
                        </div>
                    `);
                }
            }

            // 开始轮询支付状态
            startPaymentPolling(result.data.prepayId);
        } else {
            $('#payment-qr-code').html(`
                <div class="error-message">
                    <i class="fas fa-exclamation-triangle"></i>
                    <p>${result.message || '创建订单失败'}</p>
                </div>
            `);
        }
    } catch (error) {
        console.error('创建订单失败:', error);
        $('#payment-qr-code').html(`
            <div class="error-message">
                <i class="fas fa-exclamation-triangle"></i>
                <p>网络错误，请重试</p>
            </div>
        `);
    }
}

// 轮询支付状态
//let pollingInterval = null;
//let pollingCount = 0;
//const MAX_POLLING_COUNT = 60; // 最多轮询3分钟（60次 * 3秒）

//function startPaymentPolling(prepayId) {
//    pollingCount = 0;
//    pollingInterval = setInterval(async () => {
//        pollingCount++;

//        try {
//            // 检查用户积分是否增加，以此判断支付是否成功
//            const response = await fetch('/api/userprofile/info', {
//                headers: {
//                    'Authorization': 'Bearer ' + getToken()
//                }
//            });

//            if (response.ok) {
//                const result = await response.json();
//                if (result.success) {
//                    const newBalance = result.data.points;
//                    const oldBalance = parseInt($('#user-points-balance').text());

//                    if (newBalance > oldBalance) {
//                        // 支付成功
//                        stopPolling();
//                        onPaymentSuccess();
//                    } else if (pollingCount >= MAX_POLLING_COUNT) {
//                        // 超时
//                        stopPolling();
//                        $('#payment-qr-code').html(`
//                            <div class="success-message">
//                                <i class="fas fa-info-circle" style="color: var(--info-color); font-size: 4rem;"></i>
//                                <p style="margin-top: var(--spacing-md); font-weight: 600;">支付完成确认</p>
//                                <p style="font-size: 0.875rem; color: var(--text-secondary); margin-bottom: var(--spacing-lg);">
//                                    如果您已完成支付，请点击下方按钮确认
//                                </p>
//                                <button class="btn btn-primary" onclick="confirmPaymentSuccess()">
//                                    我已支付完成
//                                </button>
//                                <button class="btn btn-secondary" onclick="closePaymentModal(); loadPaymentOrders();" style="margin-top: var(--spacing-sm);">
//                                    取消支付
//                                </button>
//                            </div>
//                        `);
//                    }
//                }
//            }
//        } catch (error) {
//            console.error('检查支付状态失败:', error);
//        }
//    }, 3000); // 每3秒检查一次
//}

// 轮询支付状态
let pollingInterval = null;
let pollingCount = 0;
const MAX_POLLING_COUNT = 60; // 最多轮询3分钟（60次 * 3秒）

function startPaymentPolling(prepayId) {
    // 先获取当前积分作为基准
    const oldBalance = parseInt($('#user-points-balance').text());
    console.log('开始轮询，当前积分:', oldBalance);

    pollingCount = 0;
    pollingInterval = setInterval(async () => {
        pollingCount++;
        console.log(`轮询第 ${pollingCount} 次`);

        try {
            // 检查用户积分是否增加
            const response = await fetch('/api/userprofile/info', {
                headers: {
                    'Authorization': 'Bearer ' + getToken()
                }
            });

            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    const newBalance = result.data.points;
                    console.log(`当前积分: ${newBalance}, 原积分: ${oldBalance}`);

                    if (newBalance > oldBalance) {
                        // 支付成功
                        console.log('检测到积分增加，支付成功！');
                        stopPolling();
                        onPaymentSuccess();
                    } else if (pollingCount >= MAX_POLLING_COUNT) {
                        // 超时
                        console.log('轮询超时');
                        stopPolling();
                        $('#payment-qr-code').html(`
                            <div class="success-message">
                                <i class="fas fa-info-circle" style="color: var(--info-color); font-size: 4rem;"></i>
                                <p style="margin-top: var(--spacing-md); font-weight: 600;">支付完成确认</p>
                                <p style="font-size: 0.875rem; color: var(--text-secondary); margin-bottom: var(--spacing-lg);">
                                    如果您已完成支付，请点击下方按钮确认
                                </p>
                                <button class="btn btn-primary" onclick="confirmPaymentSuccess()">
                                    我已支付完成
                                </button>
                                <button class="btn btn-secondary" onclick="closePaymentModal(); loadPaymentOrders();" style="margin-top: var(--spacing-sm);">
                                    取消支付
                                </button>
                            </div>
                        `);
                    }
                }
            }
        } catch (error) {
            console.error('检查支付状态失败:', error);
        }
    }, 3000); // 每3秒检查一次
}


// 手动确认支付成功
async function confirmPaymentSuccess() {
    try {
        // 强制刷新用户信息
        const response = await fetch('/api/userprofile/info', {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                const newBalance = result.data.points;
                const oldBalance = parseInt($('#user-points-balance').text());

                if (newBalance > oldBalance) {
                    onPaymentSuccess();
                } else {
                    // 如果积分没有变化，显示手动处理提示
                    $('#payment-qr-code').html(`
                        <div class="success-message">
                            <i class="fas fa-check-circle" style="color: var(--success-color); font-size: 4rem;"></i>
                            <p style="margin-top: var(--spacing-md); font-weight: 600;">支付确认成功！</p>
                            <p style="font-size: 0.875rem; color: var(--text-secondary);">
                                积分将在后台处理完成后到账，请稍后刷新页面查看
                            </p>
                            <button class="btn btn-primary" onclick="closePaymentModal(); location.reload();" style="margin-top: var(--spacing-md);">
                                确定
                            </button>
                        </div>
                    `);
                }
            }
        }
    } catch (error) {
        console.error('确认支付失败:', error);
        alert('确认失败，请刷新页面查看订单状态');
        closePaymentModal();
    }
}

function stopPolling() {
    if (pollingInterval) {
        clearInterval(pollingInterval);
        pollingInterval = null;
    }
    pollingCount = 0;
}

// 支付成功
function onPaymentSuccess() {
    $('#payment-qr-code').html(`
        <div class="success-message">
            <i class="fas fa-check-circle" style="color: var(--success-color); font-size: 4rem;"></i>
            <p style="margin-top: var(--spacing-md); font-weight: 600;">支付成功！</p>
            <p style="font-size: 0.875rem; color: var(--text-secondary);">积分已到账</p>
        </div>
    `);

    // 刷新用户信息和订单列表
    loadUserInfo();
    loadPaymentOrders();

    // 2秒后关闭弹窗并刷新页面
    setTimeout(() => {
        closePaymentModal();
        // 刷新整个页面以确保数据更新
        location.reload();
    }, 2000);
}

// 关闭支付弹窗
function closePaymentModal() {
    stopPolling();
    $('#paymentModal').fadeOut(function () {
        $(this).css('display', 'none'); // 确保完全隐藏
    });
    currentPackage = null;
}

// 加载支付订单列表
async function loadPaymentOrders(page = 1) {
    currentPage = page;

    try {
        const response = await fetch(`/api/pointpurchase/orders?page=${page}&pageSize=${pageSize}`, {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        if (response.status === 401) {
            handleUnauthorized(response);
            return;
        }

        const result = await response.json();

        if (result.success && result.data.length > 0) {
            totalOrders = result.pagination.total;
            
            const ordersHtml = result.data.map(order => {
                const statusClass = getOrderStatusClass(order.orderStatus);
                const statusText = getOrderStatusText(order.orderStatus);
                
                return `
                    <div class="order-item">
                        <div class="order-header">
                            <div>
                                <span style="font-weight: 600;">订单号: ${order.orderNo}</span>
                                <span style="font-size: 0.875rem; color: var(--text-muted); margin-left: var(--spacing-sm);">
                                    ${formatDate(order.createdAt)}
                                </span>
                            </div>
                            <span class="badge ${statusClass}">${statusText}</span>
                        </div>
                        <div class="order-body">
                            <div class="order-info">
                                <div>
                                    <span style="color: var(--text-secondary);">充值积分:</span>
                                    <span style="font-weight: 600;">${order.points} 积分</span>
                                </div>
                                <div>
                                    <span style="color: var(--text-secondary);">支付金额:</span>
                                    <span style="font-weight: 600; color: var(--primary-color);">¥${order.amount.toFixed(2)}</span>
                                </div>
                            </div>
                            ${order.transactionId ? `<div style="font-size: 0.8125rem; color: var(--text-muted);">交易号: ${order.transactionId}</div>` : ''}
                        </div>
                    </div>
                `;
            }).join('');

            $('#orders-container').html(ordersHtml);
            renderPagination(result.pagination.totalPages);
        } else {
            $('#orders-container').html(`
                <div class="empty-state">
                    <i class="fas fa-file-invoice-dollar"></i>
                    <p>暂无充值记录</p>
                </div>
            `);
            $('#paginationContainer').hide();
        }
    } catch (error) {
        console.error('加载订单失败:', error);
        $('#orders-container').html(`
            <div class="empty-state">
                <i class="fas fa-exclamation-circle"></i>
                <p>加载失败，请刷新重试</p>
            </div>
        `);
        $('#paginationContainer').hide();
    }
}

// 获取订单状态样式类
function getOrderStatusClass(status) {
    const statusMap = {
        'Pending': 'badge-warning',
        'Paid': 'badge-success',
        'Failed': 'badge-danger',
        'Cancelled': 'badge-info',
        'Refunded': 'badge-info'
    };
    return statusMap[status] || 'badge-info';
}

// 获取订单状态文本
function getOrderStatusText(status) {
    const statusMap = {
        'Pending': '待支付',
        'Paid': '已支付',
        'Failed': '支付失败',
        'Cancelled': '已取消',
        'Refunded': '已退款'
    };
    return statusMap[status] || status;
}

// 渲染分页
function renderPagination(totalPages) {
    if (totalPages <= 1) {
        $('#paginationContainer').hide();
        return;
    }

    $('#paginationContainer').show();
    
    let html = `
        <button class="btn" ${currentPage === 1 ? 'disabled' : ''} onclick="loadPaymentOrders(${currentPage - 1})">
            <i class="fas fa-chevron-left"></i>
        </button>
    `;

    for (let i = 1; i <= totalPages; i++) {
        if (i === 1 || i === totalPages || (i >= currentPage - 1 && i <= currentPage + 1)) {
            html += `
                <button class="btn ${i === currentPage ? 'btn-primary' : ''}" onclick="loadPaymentOrders(${i})">${i}</button>
            `;
        } else if (i === currentPage - 2 || i === currentPage + 2) {
            html += '<span>...</span>';
        }
    }

    html += `
        <button class="btn" ${currentPage === totalPages ? 'disabled' : ''} onclick="loadPaymentOrders(${currentPage + 1})">
            <i class="fas fa-chevron-right"></i>
        </button>
    `;

    $('#pagination').html(html);
}

// 格式化日期
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// 切换到绘图页面
function switchToDrawing() {
    window.location.href = '/Home/Drawing';
}

// 切换到历史页面
function switchToHistory() {
    window.location.href = '/Home/History';
}

// 跳转到用户信息页面
function switchToProfile() {
    window.location.href = '/Home/Profile';
}
