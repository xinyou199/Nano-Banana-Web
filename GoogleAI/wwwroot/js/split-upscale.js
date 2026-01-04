// 拆分重绘功能 JavaScript
// 作者：AI绘图平台
// 功能：图片拆分和高清重绘

// 全局变量
let splitUpscaleState = {
    currentTaskId: null,
    currentImageUrl: null,
    currentTask: null,
    imageAspectRatio: 1,
    splitMode: '3x3',
    selectedBlocks: new Set(),
    selectedFaceReferenceImage: null,
    models: [],
    selectedModelId: null,
    batchGroupId: null,
    progressInterval: null,
    isOnlySplit: false,
    tolerance: 0.02,
    processMode: 'split-upscale'
};

/**
 * 打开拆分重绘模态框
 */
async function openSplitUpscaleModal(taskId, imageUrl) {
    splitUpscaleState.currentTaskId = taskId;
    splitUpscaleState.currentImageUrl = imageUrl;
    splitUpscaleState.selectedBlocks.clear();
    splitUpscaleState.selectedFaceReferenceImage = null;
    
    // ✅ 立即显示模态框，显示加载状态
    const modal = document.getElementById('splitUpscaleModal');
    modal.style.display = 'flex';
    
    // 在模态框中显示加载状态
    const modalBody = modal.querySelector('.modal-body');
    const originalContent = modalBody.innerHTML;
    modalBody.innerHTML = `
        <div style="text-align: center; padding: 40px 20px;">
            <i class="fas fa-spinner fa-spin" style="font-size: 2.5rem; color: var(--primary-color); margin-bottom: 16px; display: block;"></i>
            <p style="color: var(--text-secondary); font-size: 1rem; margin: 0;">正在加载拆分重绘数据...</p>
        </div>
    `;
    
    try {
        // ✅ 后台并行加载所有数据
        await Promise.all([
            loadTaskDetails(taskId),
            calculateImageAspectRatio(imageUrl),
            loadModelsForSplit()
        ]);
        
        // 所有数据加载完成后，恢复原始内容并进行渲染
        modalBody.innerHTML = originalContent;
        
        // ✅ 填充模型下拉框
        const select = document.getElementById('splitModelSelect');
        if (select) {
            const models = splitUpscaleState.models;
            if (models.length === 0) {
                select.innerHTML = '<option value="">暂无可用模型</option>';
            } else {
                select.innerHTML = models.map(m => 
                    `<option value="${m.id}" data-cost="${m.pointCost}">${m.modelName} (${m.pointCost}积分)</option>`
                ).join('');
                
                // 选择第一个模型
                splitUpscaleState.selectedModelId = models[0].id;
                
                // 监听模型变化
                select.addEventListener('change', function() {
                    splitUpscaleState.selectedModelId = parseInt(this.value);
                    updateCostSummary();
                });
            }
        }
        
        // 渲染参考图选择器
        renderFaceReferenceSelector();
        
        // 渲染九宫格
        renderSplitGrid();
        
        // 默认选中所有分块
        const [rows, cols] = splitUpscaleState.splitMode.split('x').map(Number);
        const totalBlocks = rows * cols;
        for (let i = 0; i < totalBlocks; i++) {
            splitUpscaleState.selectedBlocks.add(i);
        }
        
        // 重新渲染九宫格以显示选中状态
        renderSplitGrid();
        
        // 更新成本显示
        updateCostSummary();
    } catch (error) {
        console.error('加载拆分重绘数据失败:', error);
        modalBody.innerHTML = `
            <div style="text-align: center; padding: 40px 20px;">
                <i class="fas fa-exclamation-circle" style="font-size: 2.5rem; color: var(--error-color); margin-bottom: 16px; display: block;"></i>
                <p style="color: var(--error-color); font-size: 1rem; margin: 0;">加载数据失败，请关闭后重试</p>
                <p style="color: var(--text-secondary); font-size: 0.875rem; margin-top: 8px;">${error.message}</p>
            </div>
        `;
    }
}

/**
 * 加载任务详情
 */
async function loadTaskDetails(taskId) {
    try {
        const response = await fetch(`/api/tasks/${taskId}`, {
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });
        
        if (response.ok) {
            const result = await response.json();
            if (result) {
                splitUpscaleState.currentTask = result;
            }
        }
    } catch (error) {
        console.error('加载任务详情失败:', error);
    }
}

/**
 * 计算图片宽高比
 */
function calculateImageAspectRatio(imageUrl) {
    return new Promise((resolve) => {
        const img = new Image();
        img.onload = function() {
            splitUpscaleState.imageAspectRatio = this.width / this.height;
            console.log('图片宽高比:', splitUpscaleState.imageAspectRatio);
            resolve();
        };
        img.onerror = function() {
            splitUpscaleState.imageAspectRatio = 1; // 默认正方形
            resolve();
        };
        img.src = imageUrl;
    });
}

/**
 * 渲染参考图选择器
 */
function renderFaceReferenceSelector() {
    const container = document.getElementById('faceReferenceContainer');
    if (!container) return;
    
    const task = splitUpscaleState.currentTask;
    
    // 解析参考图（可能是JSON字符串）
    let referenceImages = [];
    if (task && task.referenceImages) {
        try {
            if (typeof task.referenceImages === 'string') {
                referenceImages = JSON.parse(task.referenceImages);
            } else if (Array.isArray(task.referenceImages)) {
                referenceImages = task.referenceImages;
            }
        } catch (e) {
            console.error('解析参考图失败:', e);
        }
    }
    
    // 检查是否有参考图
    if (!referenceImages || referenceImages.length === 0) {
        container.innerHTML = `
            <div style="padding: 10px; color: var(--text-secondary); font-size: 0.875rem; text-align: center;">
                <i class="fas fa-info-circle"></i> 原始任务没有上传参考图
            </div>
        `;
        return;
    }
    
    // 渲染参考图选择
    const imagesHTML = referenceImages.map((imgUrl, index) => `
        <div class="face-ref-item ${splitUpscaleState.selectedFaceReferenceImage === imgUrl ? 'selected' : ''}" 
             onclick="selectFaceReference('${imgUrl}')">
            <img src="${imgUrl}" alt="参考图 ${index + 1}">
            <div class="face-ref-check">
                <i class="fas fa-check"></i>
            </div>
        </div>
    `).join('');
    
    container.innerHTML = `
        <div class="face-ref-grid">
            <div class="face-ref-item ${!splitUpscaleState.selectedFaceReferenceImage ? 'selected' : ''}" 
                 onclick="selectFaceReference(null)">
                <div class="face-ref-none">
                    <i class="fas fa-ban"></i>
                    <span>不使用</span>
                </div>
                <div class="face-ref-check">
                    <i class="fas fa-check"></i>
                </div>
            </div>
            ${imagesHTML}
        </div>
    `;
}

/**
 * 选择脸部参考图
 */
function selectFaceReference(imageUrl) {
    splitUpscaleState.selectedFaceReferenceImage = imageUrl;
    renderFaceReferenceSelector();
}

/**
 * 关闭拆分模态框
 */
function closeSplitModal() {
    document.getElementById('splitUpscaleModal').style.display = 'none';
    splitUpscaleState.selectedBlocks.clear();
}

/**
 * 选择拆分模式
 */
function selectSplitMode(mode) {
    splitUpscaleState.splitMode = mode;
    splitUpscaleState.selectedBlocks.clear();
    
    // 更新按钮状态
    document.querySelectorAll('.split-mode-selector .mode-btn').forEach(btn => {
        btn.classList.remove('active');
        if (btn.dataset.mode === mode) {
            btn.classList.add('active');
        }
    });
    
    // 重新渲染九宫格
    renderSplitGrid();
    
    // 更新成本
    updateCostSummary();
}

/**
 * 渲染九宫格
 */
function renderSplitGrid() {
    const [rows, cols] = splitUpscaleState.splitMode.split('x').map(Number);
    const container = document.getElementById('splitGridContainer');
    const aspectRatio = splitUpscaleState.imageAspectRatio;
    
    // 生成九宫格HTML，应用图片宽高比
    let gridHTML = `<div class="split-grid" style="--rows: ${rows}; --cols: ${cols}; --aspect-ratio: ${aspectRatio};">`;
    
    for (let row = 0; row < rows; row++) {
        for (let col = 0; col < cols; col++) {
            const index = row * cols + col;
            const isSelected = splitUpscaleState.selectedBlocks.has(index);
            
            gridHTML += `
                <div class="split-block ${isSelected ? 'selected' : ''}" 
                     data-index="${index}"
                     onclick="toggleBlock(${index})"
                     style="background-image: url('${splitUpscaleState.currentImageUrl}'); 
                            background-size: ${cols * 100}% ${rows * 100}%;
                            background-position: ${col * 100 / (cols - 1 || 1)}% ${row * 100 / (rows - 1 || 1)}%;">
                    <div class="block-index">${index + 1}</div>
                </div>
            `;
        }
    }
    
    gridHTML += '</div>';
    container.innerHTML = gridHTML;
}

/**
 * 切换分块选中状态
 */
function toggleBlock(index) {
    if (splitUpscaleState.selectedBlocks.has(index)) {
        splitUpscaleState.selectedBlocks.delete(index);
    } else {
        splitUpscaleState.selectedBlocks.add(index);
    }
    
    // 更新UI
    const block = document.querySelector(`.split-block[data-index="${index}"]`);
    if (block) {
        block.classList.toggle('selected');
    }
    
    // 更新成本
    updateCostSummary();
}

/**
 * 全选/取消全选分块
 */
function toggleAllBlocks() {
    const [rows, cols] = splitUpscaleState.splitMode.split('x').map(Number);
    const totalBlocks = rows * cols;
    
    if (splitUpscaleState.selectedBlocks.size === totalBlocks) {
        // 已全选，则清空
        splitUpscaleState.selectedBlocks.clear();
    } else {
        // 未全选，则全选
        for (let i = 0; i < totalBlocks; i++) {
            splitUpscaleState.selectedBlocks.add(i);
        }
    }
    
    renderSplitGrid();
    updateCostSummary();
}

/**
 * 加载模型列表
 */
async function loadModelsForSplit() {
    try {
        const response = await fetch('/api/Models', {
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });
        
        if (response.ok) {
            const result = await response.json();
            
            // 处理不同的API响应格式
            let models = [];
            if (result.success && result.data) {
                models = result.data;
            } else if (Array.isArray(result)) {
                models = result;
            }
            
            splitUpscaleState.models = models;
            
            // 注意：此时模态框可能显示加载状态，所以不要立即更新DOM
            // 数据会在 openSplitUpscaleModal 中的 Promise.all 完成后更新
        }
    } catch (error) {
        console.error('加载模型失败:', error);
        // 存储错误信息，稍后显示
        throw error;
    }
}

/**
 * 更新积分消耗显示
 */
function updateCostSummary() {
    const selectedCount = splitUpscaleState.selectedBlocks.size;
    const select = document.getElementById('splitModelSelect');
    const selectedOption = select.options[select.selectedIndex];
    const perBlockCost = selectedOption ? parseInt(selectedOption.dataset.cost) : 0;
    const totalCost = selectedCount * perBlockCost;
    
    document.getElementById('selectedBlockCount').textContent = selectedCount;
    document.getElementById('perBlockCost').textContent = perBlockCost;
    document.getElementById('totalCost').textContent = totalCost;
}

/**
 * 切换处理模式
 */
function switchProcessMode(mode) {
    splitUpscaleState.processMode = mode;
    splitUpscaleState.isOnlySplit = (mode === 'split-only');
    
    const toleranceGroup = document.getElementById('toleranceGroup');
    const upscaleOptionsGroup = document.querySelector('[id="splitModelSelect"]')?.closest('.form-group')?.parentElement;
    const submitBtn = document.getElementById('submitSplitBtn');
    const costSummary = document.querySelector('.cost-summary');
    
    if (mode === 'split-only') {
        // 仅拆分模式
        toleranceGroup.style.display = 'block';
        
        // 隐藏模型和参考图选择
        const modelGroup = document.querySelector('label:has(+ #splitModelSelect)')?.closest('.form-group');
        const refGroup = document.querySelector('label:has(+ #faceReferenceContainer)')?.closest('.form-group');
        if (modelGroup) modelGroup.style.display = 'none';
        if (refGroup) refGroup.style.display = 'none';
        
        submitBtn.innerHTML = '<i class="fas fa-cut"></i> 开始拆分';
        
        // 自动全选所有分块
        const [rows, cols] = splitUpscaleState.splitMode.split('x').map(Number);
        const totalBlocks = rows * cols;
        splitUpscaleState.selectedBlocks.clear();
        for (let i = 0; i < totalBlocks; i++) {
            splitUpscaleState.selectedBlocks.add(i);
        }
        renderSplitGrid();
        
        // 隐藏积分消耗提示
        if (costSummary) costSummary.style.display = 'none';
    } else {
        // 拆分重绘模式
        toleranceGroup.style.display = 'none';
        
        // 显示模型和参考图选择
        const modelGroup = document.querySelector('label:has(+ #splitModelSelect)')?.closest('.form-group');
        const refGroup = document.querySelector('label:has(+ #faceReferenceContainer)')?.closest('.form-group');
        if (modelGroup) modelGroup.style.display = 'block';
        if (refGroup) refGroup.style.display = 'block';
        
        submitBtn.innerHTML = '<i class="fas fa-magic"></i> 开始重绘';
        
        // 显示积分消耗提示
        if (costSummary) costSummary.style.display = 'block';
    }
}

/**
 * 更新容差值
 */
function updateToleranceValue(value) {
    splitUpscaleState.tolerance = parseFloat(value);
    document.getElementById('toleranceSlider').value = value;
    document.getElementById('toleranceInput').value = value;
}

/**
 * 提交拆分重绘请求
 */
async function submitSplitUpscale() {
    if (splitUpscaleState.isOnlySplit) {
        // 仅拆分模式
        await submitSplitOnly();
    } else {
        // 拆分重绘模式（现有逻辑）
        await submitSplitUpscaleOriginal();
    }
}

/**
 * 仅拆分模式提交
 */
async function submitSplitOnly() {
    const submitBtn = document.getElementById('submitSplitBtn');
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> 处理中...';
    
    try {
        const requestBody = {
            originalTaskId: splitUpscaleState.currentTaskId,
            splitMode: splitUpscaleState.splitMode,
            selectedBlocks: Array.from(splitUpscaleState.selectedBlocks),
            processMode: 'split-only',
            tolerance: splitUpscaleState.tolerance
        };
        
        const response = await fetch('/api/SplitUpscale/split-and-upscale', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${getToken()}`
            },
            body: JSON.stringify(requestBody)
        });
        
        const result = await response.json();
        
        if (result.success) {
            showNotification('拆分完成！' + result.message, 'success');
            closeSplitModal();
            
            // 显示完成提示（不需要进度跟踪，因为已经完成）
            setTimeout(() => {
                showNotification(`已生成 ${result.totalBlocks} 个分块任务`, 'success');
                refreshTaskList();
            }, 500);
        } else {
            showNotification(result.message || '拆分失败', 'error');
        }
    } catch (error) {
        console.error('拆分失败:', error);
        showNotification('拆分失败: ' + error.message, 'error');
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = '<i class="fas fa-cut"></i> 开始拆分';
    }
}

/**
 * 拆分重绘模式提交（原有逻辑）
 */
async function submitSplitUpscaleOriginal() {
    // 验证
    if (splitUpscaleState.selectedBlocks.size === 0) {
        showNotification('请至少选择一个分块进行重绘', 'warning');
        return;
    }
    
    if (!splitUpscaleState.selectedModelId) {
        showNotification('请选择重绘模型', 'warning');
        return;
    }
    
    // 禁用按钮
    const submitBtn = document.getElementById('submitSplitBtn');
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> 处理中...';
    
    try {
        // 构建请求体
        const requestBody = {
            originalTaskId: splitUpscaleState.currentTaskId,
            splitMode: splitUpscaleState.splitMode,
            selectedBlocks: Array.from(splitUpscaleState.selectedBlocks),
            modelId: splitUpscaleState.selectedModelId,
            processMode: 'split-upscale'
        };
        
        // 如果选择了脸部参考图，添加到请求中
        if (splitUpscaleState.selectedFaceReferenceImage) {
            requestBody.referenceImageUrl = splitUpscaleState.selectedFaceReferenceImage;
        }
        
        const response = await fetch('/api/SplitUpscale/split-and-upscale', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${getToken()}`
            },
            body: JSON.stringify(requestBody)
        });
        
        const result = await response.json();
        
        if (result.success) {
            showNotification(result.message, 'success');
            
            // ✅ 立即关闭拆分模态框
            closeSplitModal();
            
            // ✅ 回到任务列表顶部
            setTimeout(() => {
                const scrollContainer = document.querySelector('.card-content');
                if (scrollContainer) {
                    scrollContainer.scrollTop = 0;
                }
            }, 100);
            
            // 打开进度跟踪模态框
            splitUpscaleState.batchGroupId = result.batchGroupId;
            showBatchProgressModal(result.batchGroupId);
        } else {
            showNotification(result.message || '拆分重绘失败', 'error');
        }
    } catch (error) {
        console.error('拆分重绘失败:', error);
        showNotification('拆分重绘失败: ' + error.message, 'error');
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = '<i class="fas fa-magic"></i> 开始重绘';
    }
}

/**
 * 显示批量任务进度模态框
 */
function showBatchProgressModal(batchGroupId) {
    splitUpscaleState.batchGroupId = batchGroupId;
    document.getElementById('batchProgressModal').style.display = 'flex';
    
    // 立即加载一次进度
    refreshBatchProgress();
    
    // 启动定时刷新（每3秒）
    if (splitUpscaleState.progressInterval) {
        clearInterval(splitUpscaleState.progressInterval);
    }
    splitUpscaleState.progressInterval = setInterval(() => {
        refreshBatchProgress();
    }, 3000);
}

/**
 * 关闭批量进度模态框
 */
function closeBatchProgressModal() {
    document.getElementById('batchProgressModal').style.display = 'none';
    
    // 停止定时刷新
    if (splitUpscaleState.progressInterval) {
        clearInterval(splitUpscaleState.progressInterval);
        splitUpscaleState.progressInterval = null;
    }
    
    // 刷新任务列表和历史记录
    if (typeof refreshTaskList === 'function') refreshTaskList();
    if (typeof loadHistory === 'function') loadHistory();
}

/**
 * 刷新批量任务进度
 */
async function refreshBatchProgress() {
    if (!splitUpscaleState.batchGroupId) return;
    
    try {
        const response = await fetch(`/api/SplitUpscale/batch-progress/${splitUpscaleState.batchGroupId}`, {
            headers: {
                'Authorization': `Bearer ${getToken()}`
            }
        });
        
        if (response.ok) {
            const data = await response.json();
            
            // 更新统计信息
            document.getElementById('batchTotalBlocks').textContent = data.totalBlocks;
            document.getElementById('batchCompletedBlocks').textContent = data.completedBlocks;
            document.getElementById('batchProcessingBlocks').textContent = data.processingBlocks;
            document.getElementById('batchPendingBlocks').textContent = data.pendingBlocks;
            document.getElementById('batchFailedBlocks').textContent = data.failedBlocks;
            
            // 更新进度条
            const progressFill = document.getElementById('batchProgressFill');
            const progressText = document.getElementById('batchProgressText');
            progressFill.style.width = data.progress + '%';
            progressText.textContent = data.progress + '%';
            
            // 渲染分块列表
            renderBatchBlocksList(data.blocks);
            
            // 如果全部完成，停止刷新
            if (data.isCompleted) {
                if (splitUpscaleState.progressInterval) {
                    clearInterval(splitUpscaleState.progressInterval);
                    splitUpscaleState.progressInterval = null;
                }
                showNotification('所有分块重绘完成！', 'success');
            }
        }
    } catch (error) {
        console.error('刷新进度失败:', error);
    }
}

/**
 * 渲染分块列表
 */
function renderBatchBlocksList(blocks) {
    const container = document.getElementById('batchBlocksList');
    
    if (!blocks || blocks.length === 0) {
        container.innerHTML = '<div class="empty-state">暂无分块信息</div>';
        return;
    }
    
    const html = blocks.map(block => {
        const statusClass = getStatusClass(block.status);
        const statusIcon = getStatusIcon(block.status);
        const statusText = getStatusText(block.status);
        
        return `
            <div class="batch-block-item ${statusClass}">
                <div class="block-header">
                    <div class="block-title">
                        <i class="${statusIcon}"></i>
                        <span>分块 #${block.blockIndex + 1}</span>
                    </div>
                    <div class="block-status">${statusText}</div>
                </div>
                <div class="block-progress">
                    <div class="progress-bar">
                        <div class="progress-fill" style="width: ${block.progress}%"></div>
                    </div>
                    <span class="progress-text">${block.progress}%</span>
                </div>
                ${block.progressMessage ? `<div class="block-message">${block.progressMessage}</div>` : ''}
                ${block.errorMessage ? `<div class="block-error"><i class="fas fa-exclamation-triangle"></i> ${block.errorMessage}</div>` : ''}
                ${block.resultImageUrl ? `
                    <div class="block-result">
                        <img src="${block.resultImageUrl}" alt="分块结果" onclick="openImageModal('${block.resultImageUrl}')">
                    </div>
                ` : ''}
            </div>
        `;
    }).join('');
    
    container.innerHTML = html;
}

/**
 * 获取状态样式类
 */
function getStatusClass(status) {
    const classMap = {
        'Completed': 'status-completed',
        'Processing': 'status-processing',
        'Pending': 'status-pending',
        'Failed': 'status-failed'
    };
    return classMap[status] || '';
}

/**
 * 获取状态图标
 */
function getStatusIcon(status) {
    const iconMap = {
        'Completed': 'fas fa-check-circle',
        'Processing': 'fas fa-spinner fa-spin',
        'Pending': 'fas fa-clock',
        'Failed': 'fas fa-times-circle'
    };
    return iconMap[status] || 'fas fa-question-circle';
}

/**
 * 获取状态文本
 */
function getStatusText(status) {
    const textMap = {
        'Completed': '已完成',
        'Processing': '处理中',
        'Pending': '等待中',
        'Failed': '失败'
    };
    return textMap[status] || status;
}

/**
 * 获取Token（需要在auth.js中实现）
 */
function getToken() {
    return localStorage.getItem('token') || '';
}

/**
 * 显示通知（需要在main.js中实现，如果不存在则使用showToast或alert）
 */
function showNotification(message, type = 'info') {
    // ✅ 优先使用main.js中的showToast函数
    if (typeof showToast === 'function') {
        showToast(message, type);
    } else {
        // 备用方案：使用alert
        alert(message);
    }
}

// 页面加载完成后的初始化
document.addEventListener('DOMContentLoaded', function() {
    // 为历史记录中的已完成任务添加拆分按钮
    // 这部分逻辑需要在main.js的历史记录渲染中集成
    console.log('拆分重绘功能已加载');
});
