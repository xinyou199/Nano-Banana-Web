// main.js - 整合版本
// 此文件需要整合 drawing.js 和 history.js 的所有代码

// ==================== 全局变量 ====================
// Drawing相关
let currentMode = 'generate';
let models = [];
let templates = {};
let referenceImageFiles = [];
let taskPollingInterval = null;
let allTasks = [];
let userScrollPosition = 0;
let isLoadingTasks = false; // ✅ 添加加载状态标志，防止重复加载

// History相关
let currentPage = 1;
let pageSize = 12;
let totalPages = 1;
let totalCount = 0;

// 主标签
let activeMainTab = 'drawing';

// ==================== 页面初始化 ====================
$(document).ready(function () {
    // 移除前端 checkAuth 调用，使用服务端验证

    const user = JSON.parse(localStorage.getItem('user') || '{}');
    $('#username-display .username-text').text(user.username || '用户');

    // 初始化绘图功能
    initializeDrawing();
});

// ==================== 主标签切换 ====================
function switchMainTab(tabName) {


    // 更新标签状态
    $('.main-nav-tab-integrated').removeClass('active');
    $(`.main-nav-tab-integrated[data-tab="${tabName}"]`).addClass('active');

    // 隐藏所有内容
    $('.main-content').removeClass('active');

    // 显示当前内容
    $(`#${tabName}-content`).addClass('active');

    activeMainTab = tabName;

    // 根据标签执行相应的初始化
    if (tabName === 'history') {
        loadHistory(1);
    } else if (tabName === 'drawing') {
        // 刷新任务列表
        loadTaskList(false);
        checkAndStartPolling();
    }
}

// 显示"敬请期待"提示
function showComingSoon(featureName) {
    showToast(`${featureName}功能正在开发中，敬请期待！`, 'info');
}

// ==================== Toast提示功能 ====================
function showToast(message, type = 'info') {
    $('.toast-message').remove();

    const iconMap = {
        'success': 'fa-check-circle',
        'error': 'fa-exclamation-circle',
        'info': 'fa-info-circle',
        'warning': 'fa-exclamation-triangle'
    };

    const colorMap = {
        'success': '#10b981',
        'error': '#ef4444',
        'info': '#3b82f6',
        'warning': '#f59e0b'
    };

    const toast = $(`
        <div class="toast-message" style="
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${colorMap[type]};
            color: white;
            padding: 12px 24px;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            display: flex;
            align-items: center;
            gap: 10px;
            z-index: 10000;
            animation: slideInRight 0.3s ease-out;
            font-size: 14px;
        ">
            <i class="fas ${iconMap[type]}"></i>
            <span>${message}</span>
        </div>
    `);

    $('body').append(toast);

    setTimeout(() => {
        toast.css('animation', 'slideOutRight 0.3s ease-out');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// ==================== 样式添加 ====================
function addHistoryPromptStyles() {
    if ($('#history-prompt-styles').length) return;
    $('head').append(`
        <style id="history-prompt-styles">
        .gallery-prompt-wrapper {
            width: 100%;
        }
        
        .gallery-prompt-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            gap: 12px;
            cursor: pointer;
            padding: 10px 12px;
            background: linear-gradient(135deg, rgba(99, 102, 241, 0.03), rgba(168, 85, 247, 0.03));
            border-radius: var(--radius-sm);
            border: 1px solid rgba(99, 102, 241, 0.15);
            margin-bottom: 10px;
            transition: all 0.3s ease;
            font-size: 0.875rem;
            font-weight: 500;
        }
        
        .gallery-prompt-header:hover {
            background: linear-gradient(135deg, rgba(99, 102, 241, 0.06), rgba(168, 85, 247, 0.06));
            border-color: rgba(99, 102, 241, 0.3);
        }
        
        .gallery-prompt-title {
            color: var(--primary-color);
            display: flex;
            align-items: center;
            gap: 6px;
            flex-shrink: 0;
            font-weight: 600;
            font-size: 0.875rem;
            white-space: nowrap;
        }
        
        .gallery-prompt-title::before {
            content: '\\f4ad';
            font-family: 'Font Awesome 6 Free';
            font-weight: 400;
            font-size: 0.875rem;
        }
        
        .gallery-prompt {
            background: rgba(99, 102, 241, 0.02);
            padding: 10px 12px;
            border-radius: var(--radius-sm);
            border: 1px solid rgba(99, 102, 241, 0.08);
            margin-top: 10px;
            line-height: 1.8;
            word-wrap: break-word;
            text-align: left;
            position: relative;
            font-size: 0.875rem;
            overflow: hidden;
            transition: all 0.3s ease;
            color: var(--text-secondary);
            direction: ltr;
            unicode-bidi: plaintext;
            user-select: text;
            -webkit-user-select: text;
            -moz-user-select: text;
            -ms-user-select: text;
            cursor: text;
            max-height: 300px;
            overflow-y: auto;
            overflow-x: hidden;
            width: 100%;
            box-sizing: border-box;
        }
        
        .gallery-prompt.collapsed {
            max-height: 60px;
            overflow: hidden;
        }
        
        .gallery-prompt.collapsed::after {
            content: '';
            position: absolute;
            bottom: 0;
            left: 0;
            right: 0;
            height: 20px;
            background: linear-gradient(to bottom, transparent, rgba(99, 102, 241, 0.02));
            pointer-events: none;
            z-index: 1;
        }
        
        .gallery-prompt:not(.collapsed) {
            max-height: none;
        }
        
        .gallery-prompt:not(.collapsed)::after {
            display: none;
        }
        
        .prompt-toggle {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 0.8125rem;
            color: var(--primary-color);
            transition: all 0.2s ease;
            flex-shrink: 0;
            padding: 4px 8px;
            border-radius: 4px;
            background: rgba(99, 102, 241, 0.08);
            white-space: nowrap;
        }
        
        .prompt-toggle:hover {
            background: rgba(99, 102, 241, 0.15);
        }
        
        .prompt-toggle i {
            transition: transform 0.3s ease;
            font-size: 0.75rem;
        }
        
        .prompt-toggle.expanded i {
            transform: rotate(180deg);
        }
        </style>
    `);
}

function addTaskProgressStyles() {
    if ($('#task-progress-styles').length) return;
    $('head').append(`
        <style id="task-progress-styles">
        @keyframes spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
        }
        
        .prompt-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            cursor: pointer;
            padding: 8px 12px;
            background: rgba(59, 130, 246, 0.05);
            border-radius: var(--radius-sm);
            border: 1px solid rgba(59, 130, 246, 0.2);
            margin-bottom: 8px;
            transition: all 0.2s ease;
        }
        
        .prompt-header:hover {
            background: rgba(59, 130, 246, 0.1);
            border-color: rgba(59, 130, 246, 0.3);
        }
        
        .prompt-header strong {
            color: var(--primary-color);
        }
        
        .prompt-toggle {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 0.875rem;
            color: var(--primary-color);
            transition: all 0.2s ease;
        }
        
        .prompt-toggle i {
            transition: transform 0.2s ease;
        }
        
        .prompt-toggle.expanded i {
            transform: rotate(180deg);
        }
        
        .prompt-content {
            background: var(--bg-secondary);
            padding: 12px;
            border-radius: var(--radius-sm);
            border: 1px solid var(--border-color);
            margin-top: 8px;
            line-height: 1.5;
            max-height: 300px;
            overflow-y: auto;
            word-wrap: break-word;
            text-align: left;
            position: relative;
        }
        
        .prompt-content.collapsed {
            max-height: 80px;
            overflow: hidden;
            mask-image: linear-gradient(to bottom, black 70%, transparent 100%);
            -webkit-mask-image: linear-gradient(to bottom, black 70%, transparent 100%);
        }
        
        .prompt-content.collapsed::after {
            content: '';
            position: absolute;
            bottom: 0;
            left: 0;
            right: 0;
            height: 30px;
            background: linear-gradient(transparent, var(--bg-secondary));
            pointer-events: none;
        }
        </style>
    `);
}

function initializeDrawing() {
    addHistoryPromptStyles();
    addTaskProgressStyles();
    loadModels();
    loadTemplates();
    bindImageUploadEvents();
    addDragDropSupport();
    loadTaskList(true).then(() => {
        checkAndStartPolling();
        fixPromptTextDirection();
    });
}

async function loadModels() {
    try {
        const response = await fetch('/api/models', {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        // 处理401 Unauthorized错误，跳转到登录页面
        if (response.status === 401) {
            clearToken();
            alert('登录已过期，请重新登录');
            window.location.href = '/';
            return;
        }

        const result = await response.json();
        if (result.success && result.data) {
            models = result.data;
            const select = $('#model-select');
            select.empty();

            if (models.length === 0) {
                select.append('<option value="">暂无可用模型</option>');
            } else {
                models.forEach(model => {
                    const optionText = `${model.description}${' ' + model.pointCost + '积分/次'}`;
                    select.append(`<option value="${model.id}">${optionText}</option>`);
                });
            }
        }
    } catch (error) {

        $('#model-select').html('<option value="">加载失败，请刷新重试</option>');
    }
}

// ==================== 模式切换 ====================
function switchMode(mode) {
    currentMode = mode;

    // 隐藏所有模式内容
    $('.mode-content').hide();

    // 显示当前模式
    $(`#${mode}Mode`).show();

    // 更新按钮状态
    $('.mode-grid .nav-tab').removeClass('active');
    event.target.classList.add('active');

    // 重新渲染模板
    renderTemplates();

    // 添加切换动画
    $(`#${mode}Mode`).css('opacity', '0').animate({ opacity: 1 }, 300);
}

// ==================== 图片比例处理 ====================
function handleAspectRatioPresetChange() {
    const preset = $('#aspectRatioPreset').val();
    const customSection = $('#customAspectRatio');

    if (preset === 'custom') {
        customSection.show();
        // 设置默认值
        $('#aspectWidth').val('16');
        $('#aspectHeight').val('9');
    } else {
        customSection.hide();
        // 如果选择了预设，更新自定义输入框的值
        if (preset && preset.includes(':')) {
            const [width, height] = preset.split(':');
            $('#aspectWidth').val(width);
            $('#aspectHeight').val(height);
        }
    }
}

// 获取当前选择的图片比例
function getCurrentAspectRatio() {
    const preset = $('#aspectRatioPreset').val();
    if (preset && preset !== 'custom') {
        return preset;
    }

    const width = $('#aspectWidth').val() || '16';
    const height = $('#aspectHeight').val() || '9';
    return `${width}:${height}`;
}

// 验证自定义比例
function validateAspectRatio() {
    const width = parseInt($('#aspectWidth').val());
    const height = parseInt($('#aspectHeight').val());

    if (isNaN(width) || isNaN(height) || width < 1 || height < 1) {
        showToast('请输入有效的图片比例（宽度、高度都必须大于0）', 'error');
        return false;
    }

    if (width > 100 || height > 100) {
        showToast('图片比例数值不能超过100', 'warning');
        return false;
    }

    return true;
}

// ==================== 执行绘图 ====================
async function executeDrawing() {
    const modelId = parseInt($('#model-select').val());
    if (!modelId) {
        alert('请选择AI模型');
        return;
    }

    let prompt = '';
    const imageFiles = [];

    // 根据不同模式获取prompt和图片
    switch (currentMode) {
        case 'generate':
            prompt = $('#prompt').val().trim();
            if (referenceImageFiles.length > 0) {
                imageFiles.push(...referenceImageFiles);
            }
            break;
        case 'localEdit':
            prompt = $('#localEditPrompt').val().trim();
            const editImage = $('#localEditImage')[0].files[0];
            if (editImage) imageFiles.push(editImage);
            break;
        case 'fusion':
            prompt = $('#fusionPrompt').val().trim();
            const mainImage = $('#fusionMainImage')[0].files[0];
            const secondImage = $('#fusionSecondImage')[0].files[0];
            if (mainImage) imageFiles.push(mainImage);
            if (secondImage) imageFiles.push(secondImage);
            break;
        case 'textEdit':
            prompt = $('#textEditPrompt').val().trim();
            const textImage = $('#textEditImage')[0].files[0];
            if (textImage) imageFiles.push(textImage);
            break;
        case 'consistency':
            prompt = $('#consistencyPrompt').val().trim();
            const consistImage = $('#consistencyRefImage')[0].files[0];
            if (consistImage) imageFiles.push(consistImage);
            break;
    }

    if (!prompt) {
        alert('请输入创意描述');
        return;
    }

    // 获取并验证图片比例
    const aspectRatio = getCurrentAspectRatio();
    if (!validateAspectRatio()) {
        return;
    }

    try {
        // 只禁用按钮，显示提交状态
        $('#executeBtn').prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> 提交中...');

        // 使用FormData上传图片
        const formData = new FormData();
        formData.append('modelId', modelId);
        formData.append('taskMode', currentMode);
        formData.append('prompt', prompt);
        formData.append('aspectRatio', aspectRatio);

        // 添加所有图片文件
        imageFiles.forEach((file) => {
            formData.append('images', file);
        });

        // 创建任务
        const response = await fetch('/api/tasks/create', {
            method: 'POST',
            headers: {
                'Authorization': 'Bearer ' + getToken()
            },
            body: formData
        });

        // 处理401 Unauthorized错误，跳转到登录页面
        if (response.status === 401) {
            clearToken();
            alert('登录已过期，请重新登录');
            window.location.href = '/';
            return;
        }

        const result = await response.json();

        if (result.success) {
            // 恢复按钮状态
            $('#executeBtn').prop('disabled', false).html('<i class="fas fa-magic"></i> 开始生成');

            // ✅ 修复：直接重新加载任务列表，避免重复添加
            await loadTaskList(true);

            // 开启轮询检查任务状态
            checkAndStartPolling();

            // 提示用户
            showToast('任务提交成功！正在处理中...', 'success');
        } else {


            $('#executeBtn').prop('disabled', false).html('<i class="fas fa-magic"></i> 开始生成');
            showToast('创建任务失败: ' + result.message, 'error');
        }
    } catch (error) {

        $('#executeBtn').prop('disabled', false).html('<i class="fas fa-magic"></i> 开始生成');
        showToast('提交失败: ' + error.message, 'error');
    }
}

// ==================== 轻量级提示功能 ====================
function showToast(message, type = 'info') {
    // 移除旧的提示
    $('.toast-message').remove();

    const iconMap = {
        'success': 'fa-check-circle',
        'error': 'fa-exclamation-circle',
        'info': 'fa-info-circle'
    };

    const colorMap = {
        'success': '#10b981',
        'error': '#ef4444',
        'info': '#3b82f6'
    };

    const toast = $(`
        <div class="toast-message" style="
            position: fixed;
            top: 20px;
            right: 20px;
            background: ${colorMap[type]};
            color: white;
            padding: 12px 24px;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            display: flex;
            align-items: center;
            gap: 10px;
            z-index: 10000;
            animation: slideInRight 0.3s ease-out;
            font-size: 14px;
        ">
            <i class="fas ${iconMap[type]}"></i>
            <span>${message}</span>
        </div>
    `);

    $('body').append(toast);

    // 3秒后自动消失
    setTimeout(() => {
        toast.css('animation', 'slideOutRight 0.3s ease-out');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// ==================== 结果显示 ====================
function displayResult(imageUrl) {
    const resultHtml = `
        <div style="text-align: center; animation: fadeIn 0.5s ease-out;">
            <img src="${imageUrl}" alt="生成的图片" style="max-width: 100%; border-radius: var(--radius-lg); box-shadow: var(--shadow-lg); border: 2px solid var(--border-color);">
            <div style="margin-top: var(--spacing-lg); display: flex; gap: var(--spacing-sm); justify-content: center; flex-wrap: wrap;">
                <button class="btn btn-primary" onclick="downloadImage('${imageUrl}')">
                    <i class="fas fa-download"></i> 下载图片
                </button>
                <button class="btn btn-secondary" onclick="viewImage('${imageUrl}')">
                    <i class="fas fa-eye"></i> 查看原图
                </button>
            </div>
        </div>
    `;
    $('#resultArea').html(resultHtml);
}

function showError(message) {
    const errorHtml = `
        <div class="empty-state">
            <i class="fas fa-exclamation-triangle" style="color: var(--error-color); opacity: 0.8;"></i>
            <p style="color: var(--error-color);">${message}</p>
            <button class="btn btn-secondary" onclick="location.reload()" style="margin-top: var(--spacing-md);">
                <i class="fas fa-redo"></i> 重试
            </button>
        </div>
    `;
    $('#taskListContainer').html(errorHtml);
}

async function downloadImage(url) {
    const response = await fetch(url, { mode: 'cors' });
    const blob = await response.blob();
    const blobUrl = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = blobUrl;
    a.download = 'ai_generated_' + Date.now() + '.png';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(blobUrl);
}

function viewImage(url) {
    window.open(url, '_blank');
}

// 复制提示词到剪贴板
async function copyPrompt(prompt) {
    try {
        await navigator.clipboard.writeText(prompt);

        // 显示成功提示
        showCopySuccess();
    } catch (err) {


        // 备用方案：创建临时文本域
        const textArea = document.createElement("textarea");
        textArea.value = prompt;
        textArea.style.position = "fixed";
        textArea.style.left = "-999999px";
        textArea.style.top = "-999999px";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();

        try {
            document.execCommand('copy');
            showCopySuccess();
        } catch (err) {

            alert('复制失败，请手动复制提示词');
        }

        document.body.removeChild(textArea);
    }
}

// 显示复制成功提示
function showCopySuccess() {
    // 创建临时提示元素
    const toast = document.createElement('div');
    toast.innerHTML = '<i class="fas fa-check-circle"></i> 提示词已复制到剪贴板';
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: #28a745;
        color: white;
        padding: 12px 20px;
        border-radius: 6px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        z-index: 10000;
        font-size: 14px;
        animation: slideInRight 0.3s ease-out;
    `;

    document.body.appendChild(toast);

    // 3秒后自动消失
    setTimeout(() => {
        toast.style.animation = 'slideOutRight 0.3s ease-out';
        setTimeout(() => {
            if (document.body.contains(toast)) {
                document.body.removeChild(toast);
            }
        }, 300);
    }, 3000);
}

// ==================== 图片预览功能 ====================
function bindImageUploadEvents() {
    // 图片生成模式 - 参考图片(支持多图)
    $('#referenceImage').change(async function (e) {
        if (e.target.files && e.target.files.length > 0) {
            const newFiles = Array.from(e.target.files);
            referenceImageFiles.push(...newFiles);
            await renderReferenceImages();
            e.target.value = '';
        }
    });

    // 局部编辑模式
    $('#localEditImage').change(function (e) {
        previewImage(e.target, 'localEditCanvas', true);
    });

    // 图像融合模式
    $('#fusionMainImage').change(function (e) {
        previewImage(e.target, 'fusionMainPreview');
    });
    $('#fusionSecondImage').change(function (e) {
        previewImage(e.target, 'fusionSecondPreview');
    });

    // 文本编辑模式
    $('#textEditImage').change(function (e) {
        previewImage(e.target, 'textEditPreview');
    });

    // 一致性生成模式
    $('#consistencyRefImage').change(function (e) {
        previewImage(e.target, 'consistencyRefPreview');
    });
}

function previewImage(input, previewId, isCanvas = false) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            if (isCanvas) {
                const canvas = document.getElementById(previewId);
                const ctx = canvas.getContext('2d');
                const img = new Image();
                img.onload = function () {
                    canvas.width = img.width;
                    canvas.height = img.height;
                    ctx.drawImage(img, 0, 0);
                    canvas.style.display = 'block';
                };
                img.src = e.target.result;
            } else {
                $('#' + previewId).attr('src', e.target.result).show();
            }
        };
        reader.readAsDataURL(input.files[0]);
    }
}

// 多图片预览(修复顺序问题)
async function renderReferenceImages() {
    const container = $('#referencePreviewContainer');

    if (referenceImageFiles.length === 0) {
        container.empty().hide();
        return;
    }

    container.empty().show();

    // 使用Promise.all确保顺序
    const imagePromises = referenceImageFiles.map((file, index) => {
        return new Promise((resolve) => {
            const reader = new FileReader();
            reader.onload = function (e) {
                const itemHtml = `
                    <div class="image-preview-item" data-index="${index}">
                        <img src="${e.target.result}" alt="预览图 ${index + 1}">
                        <div class="remove-btn" onclick="removeReferenceImage(${index})">
                            <i class="fas fa-times"></i>
                        </div>
                    </div>
                `;
                resolve({ index, html: itemHtml });
            };
            reader.readAsDataURL(file);
        });
    });

    // 等待所有图片读取完成,按顺序添加
    const results = await Promise.all(imagePromises);
    results.sort((a, b) => a.index - b.index);
    results.forEach(result => {
        container.append(result.html);
    });
}

function removeReferenceImage(index) {
    referenceImageFiles.splice(index, 1);
    renderReferenceImages();
}

// ==================== 拖拽上传支持 ====================
function addDragDropSupport() {
    const uploadAreas = document.querySelectorAll('.image-upload-area');

    uploadAreas.forEach(area => {
        area.addEventListener('dragover', (e) => {
            e.preventDefault();
            area.style.borderColor = 'var(--primary-color)';
            area.style.background = 'rgba(99, 102, 241, 0.1)';
        });

        area.addEventListener('dragleave', (e) => {
            e.preventDefault();
            area.style.borderColor = 'var(--border-color)';
            area.style.background = 'var(--bg-main)';
        });

        area.addEventListener('drop', (e) => {
            e.preventDefault();
            area.style.borderColor = 'var(--border-color)';
            area.style.background = 'var(--bg-main)';

            const files = e.dataTransfer.files;
            if (files.length > 0) {
                // 触发对应的input change事件
                const inputId = area.getAttribute('onclick').match(/getElementById\('([^']+)'\)/)[1];
                const input = document.getElementById(inputId);
                input.files = files;
                $(input).trigger('change');
            }
        });
    });
}

// ==================== 模板管理功能 ====================

// 默认模板
const defaultTemplates = {
    generate: [
        { title: '🌄 自动分镜', prompt: '根据以下9条分镜绘图提示词，生成一组3x3的九宫格图片，每一个格子对应一个分镜图片，每个分镜图片比例9:16，提示词如下：' },
        { title: '👤 人物肖像', prompt: '一位年轻女性的专业肖像照,柔和的光线,简洁的背景,微笑,眼神明亮,工作室灯光,高质量' },
        { title: '🚀 科幻场景', prompt: '未来城市的夜景,高耸的摩天大楼,飞行汽车在空中穿梭,霓虹灯光,赛博朋克风格,8K超高清' },
        { title: '🐱 动物画像', prompt: '一只可爱的小猫,毛茸茸的,大眼睛,坐在花园里,周围都是鲜花,温暖的阳光,清新的画风' }
    ],
    localEdit: [
        { title: '🗑️ 移除物体', prompt: '移除图片中的人物,保持背景完整' },
        { title: '➕ 添加元素', prompt: '在这件衬衫上添加条纹图案' },
        { title: '🔧 修复缺陷', prompt: '修复图片上的划痕和污渍' }
    ],
    fusion: [
        { title: '🎨 风格转换', prompt: '将第二张图的艺术风格应用到第一张图' },
        { title: '🌍 场景融合', prompt: '将第一张图的人物放入第二张图的场景中' },
        { title: '🎭 纹理混合', prompt: '融合两张图的纹理和色彩' }
    ],
    textEdit: [
        { title: '📰 修改标题', prompt: '将报纸标题改为AI时代来临' },
        { title: '🏷️ 更换标签', prompt: '修改产品包装上的文字为新品牌名' },
        { title: '🪧 店铺招牌', prompt: '更换店铺招牌为新店名' }
    ],
    consistency: [
        { title: '🏖️ 改变背景', prompt: '保持同样的角色,但将背景改为海滩日落' },
        { title: '🤸 不同动作', prompt: '让这个角色做跳跃的动作' },
        { title: '🎨 保持风格', prompt: '在新场景中保持相同的绘画风格' }
    ]
};

function loadTemplates() {
    const saved = localStorage.getItem('promptTemplates');
    templates = saved ? JSON.parse(saved) : JSON.parse(JSON.stringify(defaultTemplates));
    renderTemplates();
}

function renderTemplates() {
    const container = $('#promptTemplates');
    const modeTemplates = templates[currentMode] || [];

    if (modeTemplates.length === 0) {
        container.html('<p style="color: var(--text-muted); text-align: center; padding: var(--spacing-lg);">暂无模板,点击"添加"创建自定义模板</p>');
        return;
    }

    container.empty();
    modeTemplates.forEach((template, index) => {
        const card = `
            <div class="prompt-template" onclick="applyTemplate(${index})">
                <span class="delete-btn" onclick="event.stopPropagation(); deleteTemplate(${index})" title="删除">
                    <i class="fas fa-times"></i>
                </span>
                <h4>${template.title}</h4>
                <p>${template.prompt}</p>
            </div>
        `;
        container.append(card);
    });
}

function applyTemplate(index) {
    const template = templates[currentMode][index];
    if (!template) return;

    switch (currentMode) {
        case 'generate':
            $('#prompt').val(template.prompt);
            break;
        case 'localEdit':
            $('#localEditPrompt').val(template.prompt);
            break;
        case 'fusion':
            $('#fusionPrompt').val(template.prompt);
            break;
        case 'textEdit':
            $('#textEditPrompt').val(template.prompt);
            break;
        case 'consistency':
            $('#consistencyPrompt').val(template.prompt);
            break;
    }

    // 滚动到输入框
    const targetId = currentMode === 'generate' ? 'prompt' : `${currentMode}Prompt`;
    document.getElementById(targetId).scrollIntoView({ behavior: 'smooth', block: 'center' });
}

function deleteTemplate(index) {
    if (confirm('确定要删除这个模板吗?')) {
        templates[currentMode].splice(index, 1);
        saveTemplates();
        renderTemplates();
    }
}

function addCustomTemplate() {
    const title = prompt('请输入模板标题:');
    if (!title) return;

    const promptText = prompt('请输入模板内容:');
    if (!promptText) return;

    if (!templates[currentMode]) {
        templates[currentMode] = [];
    }

    templates[currentMode].push({ title, prompt: promptText });
    saveTemplates();
    renderTemplates();
}

function resetTemplates() {
    if (confirm('确定要重置为默认模板吗?这将删除所有自定义模板!')) {
        templates = JSON.parse(JSON.stringify(defaultTemplates));
        saveTemplates();
        renderTemplates();
    }
}

function saveTemplates() {
    localStorage.setItem('promptTemplates', JSON.stringify(templates));
}

function exportTemplates() {
    const dataStr = JSON.stringify(templates, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'prompt-templates.json';
    link.click();
    URL.revokeObjectURL(url);
}

function importTemplates() {
    $('#templateImport').click();
}

$('#templateImport').change(function (e) {
    const file = e.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function (event) {
        try {
            const imported = JSON.parse(event.target.result);
            if (confirm('导入模板将覆盖当前所有模板,确定继续吗?')) {
                templates = imported;
                saveTemplates();
                renderTemplates();
                showToast('模板导入成功!', 'success');
            }
        } catch (error) {
            showToast('模板文件格式错误!', 'error');
        }
    };
    reader.readAsText(file);
    e.target.value = '';
});

// ==================== 任务列表功能 ====================

// 🔧 获取真正的滚动容器
function getScrollContainer() {
    const container = $('#taskListContainer');

    // 尝试多种可能的滚动容器
    const candidates = [
        container.find('.task-list'),
        container,
        container.parent(),
        container.closest('.card-content')
    ];

    for (let candidate of candidates) {
        if (candidate.length > 0) {
            const element = candidate[0];
            const overflowY = window.getComputedStyle(element).overflowY;
            const hasScroll = element.scrollHeight > element.clientHeight;

            if ((overflowY === 'auto' || overflowY === 'scroll') && hasScroll) {
                return candidate;
            }
        }
    }
    return container;
}

// 🔧 滚动到底部的通用函数
function scrollToBottom(scrollContainer, immediate = false) {
    if (!scrollContainer || scrollContainer.length === 0) {

        return;
    }

    const element = scrollContainer[0];
    const targetScroll = element.scrollHeight;



    if (immediate) {
        // 立即滚动
        element.scrollTop = targetScroll;
    } else {
        requestAnimationFrame(() => {
            element.scrollTop = element.scrollHeight;

            // 二次确认
            setTimeout(() => {
                element.scrollTop = element.scrollHeight;
            }, 100);

            // 三次确认（处理图片加载等异步情况）
            setTimeout(() => {
                element.scrollTop = element.scrollHeight;
            }, 300);
        });
    }
}

// 加载任务列表
async function loadTaskList(scrollToBottom = false) {
    try {

        const response = await fetch('/api/tasks/my?pageSize=15', {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });
        if (response.status === 401) {
            clearToken();
            //alert('登录已过期,请重新登录');
            showToast('登录已过期,请重新登录', 'success');
            setTimeout(() => {
                window.location.href = '/';
            }, 1000);
            return;
        }
        const result = await response.json();


        if (result.success && result.data) {
            // 转换PascalCase为camelCase以便前端使用
            allTasks = result.data.map(task => ({
                id: task.id,
                userId: task.userId,
                modelId: task.modelId,
                taskMode: task.taskMode,
                prompt: task.prompt,
                taskStatus: task.taskStatus,
                progress: task.progress || 0,
                progressMessage: task.progressMessage || '',
                resultImageUrl: task.resultImageUrl,
                thumbnailUrl: task.thumbnailUrl,
                errorMessage: task.errorMessage,
                referenceImages: task.referenceImages,
                createdAt: task.createdAt,
                completedAt: task.completedAt
            }));


            // 检查是否是第一次加载
            const hasExistingTasks = $('#taskListContainer .task-list').length > 0;
            // 修改滚动逻辑：新任务在顶部，所以滚动到顶部而不是底部
            const shouldScrollToTop = scrollToBottom || !hasExistingTasks;


            // 渲染任务列表
            renderTaskList(shouldScrollToTop, !hasExistingTasks);
        } else {

        }
    } catch (error) {

    } finally {
        // ✅ 重置加载状态标志
        isLoadingTasks = false;
    }
}

// 渲染任务列表
function renderTaskList(scrollToTop = false, forceRerender = false) {

    const container = $('#taskListContainer');

    // 如果不是强制重渲染且容器中有内容,使用增量更新
    if (!forceRerender && container.find('.task-list').length > 0) {

        updateTaskItemsIncrementally([...allTasks].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)), scrollToTop);
        return;
    }

    if (allTasks.length === 0) {

        if (!container.find('.empty-state').length) {
            container.html(`
                <div class="empty-state">
                    <i class="fas fa-wand-magic-sparkles"></i>
                    <p>暂无任务</p>
                    <p style="font-size: 0.875rem; margin-top: var(--spacing-sm);">提交绘图任务后将在此显示</p>
                </div>
            `);
        }
        return;
    }
    // 按创建时间降序排序(新的在前,旧的在后) - 修改任务显示顺序
    const sortedTasks = [...allTasks].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

    const tasksHtml = sortedTasks.map(task => {
        // 添加安全检查，防止 prompt 为 undefined
        const promptText = task.prompt || '';
        const promptPreview = promptText.length > 100
            ? promptText.substring(0, 100) + '...'
            : promptText;
        const promptWithBreaks = escapeHtml(promptText).replace(/\n/g, '<br>');

        // 判断是否可以删除（只有已完成和失败的任务可以删除）
        const canDelete = task.taskStatus === 'Completed' || task.taskStatus === 'Failed';

        return `
    <div class="task-item" data-task-id="${task.id}">
        <div class="task-header">
            <div class="task-header-left">
                <span class="task-status ${task.taskStatus.toLowerCase()}">${getStatusText(task.taskStatus)}</span>
                <span class="task-mode">${getModeText(task.taskMode)}</span>
                <span class="task-id-inline">任务ID: ${task.id}</span>
                <span class="task-time-inline">${formatTime(task.createdAt)}</span>
            </div>
            ${canDelete ? `
                <button class="btn-delete-task" onclick="event.stopPropagation(); deleteTask(${task.id})" title="删除任务">
                    <i class="fas fa-trash-alt"></i>
                </button>
            ` : ''}
        </div>

        <div class="task-prompt-box" onclick="togglePromptExpand(${task.id})">
            <div class="task-prompt-header">
                <div class="prompt-preview">
                    <div class="prompt-label">
                        <i class="fas fa-comment-dots"></i>
                        <span class="label-text">提示词</span>
                    </div>
                    <span id="prompt-preview-${task.id}">${escapeHtml(promptPreview)}</span>
                </div>
                <div class="prompt-toggle-btn" id="toggle-${task.id}">
                    <span id="toggle-text-${task.id}">展开</span>
                    <i class="fas fa-chevron-down"></i>
                </div>
            </div>
            <div class="task-prompt-content collapsed" id="prompt-${task.id}">
                ${promptWithBreaks}
            </div>
        </div>
        
        ${task.taskStatus === 'Processing' || task.taskStatus === 'Pending' ? `
            <div class="task-progress-indicator">
                <div class="progress-content">
                    <div class="progress-spinner"></div>
                    <div class="progress-info">
                        <div class="progress-top">
                            <span class="progress-message">${task.progressMessage || '处理中...'}</span>
                            <span class="progress-percent">${task.progress}%</span>
                        </div>
                        <div class="progress-bar-container">
                            <div class="progress-bar" style="width: ${task.progress}%;"></div>
                        </div>
                    </div>
                </div>
            </div>
        ` : ''}
        
        ${task.resultImageUrl || task.resultImageUrls ? `
            <div class="task-image-container">
                ${(() => {
                    const imageUrls = getTaskImageUrls(task);
                    if (imageUrls.length > 1) {
                        // 多张图片：显示网格
                        return generateMultiImageGrid(imageUrls, task.id, 3);
                    } else if (imageUrls.length === 1) {
                        // 单张图片：显示普通图片
                        return `
                            <img src="${task.thumbnailUrl || imageUrls[0]}" 
                                 alt="生成结果" 
                                 class="task-image" 
                                 onclick="showImageModal('${imageUrls[0]}')">
                        `;
                    }
                    return '';
                })()}
                <!-- 浮动拆分重绘按钮 -->
                <button class="floating-split-btn" 
                        onclick="event.stopPropagation(); openSplitUpscaleModal(${task.id}, '${task.resultImageUrl}')"
                        title="拆分高清重绘">
                    <i class="fas fa-th"></i>
                    <span>拆分重绘</span>
                </button>
            </div>
        ` : ''}
        
        ${task.errorMessage ? `
            <div class="task-error">
                <i class="fas fa-exclamation-circle"></i>
                <div class="error-content">
                    <strong>错误详情：</strong>
                    <span>${escapeHtml(task.errorMessage)}</span>
                </div>
            </div>
        ` : ''}
        
        ${task.resultImageUrl ? `
            <div class="task-actions-modern">
                <button class="action-btn action-btn-copy" data-prompt="${escapeHtml(task.prompt)}" onclick="event.stopPropagation(); copyPromptFromData(this)">
                    <i class="fas fa-copy"></i>
                    <span>复制提示词</span>
                </button>
                <button class="action-btn action-btn-download" onclick="event.stopPropagation(); downloadImage('${task.resultImageUrl}')">
                    <i class="fas fa-download"></i>
                    <span>下载原图</span>
                </button>
                <button class="action-btn action-btn-view" onclick="event.stopPropagation(); showImageModal('${task.resultImageUrl}')">
                    <i class="fas fa-eye"></i>
                    <span>查看原图</span>
                </button>
            </div>
        ` : `
            <div class="task-actions-modern">
                <button class="action-btn action-btn-copy" data-prompt="${escapeHtml(task.prompt)}" onclick="event.stopPropagation(); copyPromptFromData(this)">
                    <i class="fas fa-copy"></i>
                    <span>复制提示词</span>
                </button>
                ${task.taskStatus === 'Processing' || task.taskStatus === 'Pending' ? `
                    <button class="action-btn action-btn-refresh" onclick="event.stopPropagation(); checkSingleTaskStatus(${task.id})">
                        <i class="fas fa-sync-alt"></i>
                        <span>检查状态</span>
                    </button>
                ` : ''}
            </div>
        `}
    </div>
`}).join('');


    container.html(`<div class="task-list">${tasksHtml}</div>`);

    // 绑定滚动事件监听用户滚动
    const taskListElement = container.find('.task-list');
    taskListElement.off('scroll').on('scroll', function () {
        userScrollPosition = $(this).scrollTop();
    });

    // 如果需要滚动到顶部（新任务在顶部）
    if (scrollToTop) {

        // 等待DOM完全渲染和图片加载
        setTimeout(() => {
            const scrollContainer = getScrollContainer();
            scrollToTopFunc(scrollContainer, false);
        }, 200);
    } else {

        // 恢复用户之前的滚动位置
        setTimeout(() => {
            const scrollContainer = getScrollContainer();
            if (scrollContainer.length > 0) {
                scrollContainer[0].scrollTop = userScrollPosition;
            }
        }, 100);
    }
}
// 🔧 滚动到底部的通用函数 (重命名避免冲突)
function scrollToBottomFunc(scrollContainer, immediate = false) {
    if (!scrollContainer || scrollContainer.length === 0) {

        return;
    }

    const element = scrollContainer[0];
    const targetScroll = element.scrollHeight;



    if (immediate) {
        // 立即滚动
        element.scrollTop = targetScroll;
    } else {
        // 使用requestAnimationFrame确保DOM渲染完成
        requestAnimationFrame(() => {
            element.scrollTop = element.scrollHeight;

            // 二次确认
            setTimeout(() => {
                element.scrollTop = element.scrollHeight;
            }, 100);

            // 三次确认(处理图片加载等异步情况)
            setTimeout(() => {
                element.scrollTop = element.scrollHeight;
            }, 300);
        });
    }
}

// 🔧 滚动到顶部的通用函数
function scrollToTopFunc(scrollContainer, immediate = false) {
    if (!scrollContainer || scrollContainer.length === 0) {

        return;
    }

    const element = scrollContainer[0];
    const targetScroll = 0;



    if (immediate) {
        // 立即滚动
        element.scrollTop = targetScroll;
    } else {
        // 使用requestAnimationFrame确保DOM渲染完成
        requestAnimationFrame(() => {
            element.scrollTop = 0;

            // 二次确认
            setTimeout(() => {
                element.scrollTop = 0;
            }, 100);

            // 三次确认(处理图片加载等异步情况)
            setTimeout(() => {
                element.scrollTop = 0;
            }, 300);
        });
    }
}

// 增量更新任务项,避免闪烁
function updateTaskItemsIncrementally(sortedTasks, shouldScrollToTop = false) {


    const taskList = $('.task-list');
    const scrollContainer = getScrollContainer();

    if (scrollContainer.length === 0) {
        return;
    }

    const currentScrollTop = scrollContainer[0].scrollTop;
    const scrollHeight = scrollContainer[0].scrollHeight;
    const clientHeight = scrollContainer[0].clientHeight;
    const wasAtTop = currentScrollTop <= 50;
    const wasAtBottom = (currentScrollTop + clientHeight >= scrollHeight - 50);



    // 更新任务项
    sortedTasks.forEach(task => {
        const taskElement = taskList.find(`[data-task-id="${task.id}"]`);

        if (taskElement.length === 0) {
            // 新任务,添加到适当位置
            insertTaskInOrder(taskList, task);


            // 如果是新的处理中任务，显示更详细的状态
            if (task.taskStatus === 'Processing' || task.taskStatus === 'Pending') {
                showTaskProgress(task.id, task.taskStatus);
            }
        } else {
            // 更新现有任务
            updateExistingTaskItem(taskElement, task);

            // 如果是处理中任务，更新进度显示
            if (task.taskStatus === 'Processing' || task.taskStatus === 'Pending') {
                updateTaskProgress(task.id, task.taskStatus);
            } else if (task.taskStatus === 'Completed') {
                hideTaskProgress(task.id, 'success');
            } else if (task.taskStatus === 'Failed') {
                hideTaskProgress(task.id, 'error');
            }
        }
    });

    // 决定是否滚动到顶部（新任务在顶部）
    if (shouldScrollToTop || wasAtTop) {
        setTimeout(() => {
            const updatedScrollContainer = getScrollContainer();
            scrollToTopFunc(updatedScrollContainer, false);
        }, 150);
    } else {
        setTimeout(() => {
            scrollContainer[0].scrollTop = currentScrollTop;
        }, 50);
    }
}

// 按时间顺序插入新任务（新的在前）
function insertTaskInOrder(taskList, newTask) {
    const taskElements = taskList.children('.task-item');
    let insertBefore = null;

    taskElements.each(function () {
        const existingTaskId = $(this).data('task-id');
        const existingTask = allTasks.find(t => t.id === existingTaskId);

        if (existingTask && new Date(existingTask.createdAt) < new Date(newTask.createdAt)) {
            insertBefore = $(this);
            return false; // 停止迭代
        }
    });

    // 🔧 修复：将所有 task. 改为 newTask，添加安全检查
    const promptText = newTask.prompt || '';
    const promptPreview = promptText.length > 50
        ? promptText.substring(0, 50) + '...'
        : promptText;
    const promptWithBreaks = escapeHtml(promptText).replace(/\n/g, '<br>');
    
    // 判断是否可以删除（只有已完成和失败的任务可以删除）
    const canDelete = newTask.taskStatus === 'Completed' || newTask.taskStatus === 'Failed';

    const taskHtml = `
        <div class="task-item" data-task-id="${newTask.id}">
            <div class="task-header">
                <div class="task-header-left">
                    <span class="task-status ${newTask.taskStatus.toLowerCase()}">${getStatusText(newTask.taskStatus)}</span>
                    <span class="task-mode">${getModeText(newTask.taskMode)}</span>
                    <span class="task-id-inline">任务ID: ${newTask.id}</span>
                    <span class="task-time-inline">${formatTime(newTask.createdAt)}</span>
                </div>
                ${canDelete ? `
                    <button class="btn-delete-task" onclick="event.stopPropagation(); deleteTask(${newTask.id})" title="删除任务">
                        <i class="fas fa-trash-alt"></i>
                    </button>
                ` : ''}
            </div>
            
            <!-- 优化后的提示词区域 -->
            <div class="task-prompt-box" onclick="togglePromptExpand(${newTask.id})">
                <div class="task-prompt-header">
                    <div class="prompt-preview">
                        <div class="prompt-label">
                            <i class="fas fa-comment-dots"></i>
                            <span class="label-text">提示词</span>
                        </div>
                        <span id="prompt-preview-${newTask.id}">${escapeHtml(promptPreview)}</span>
                    </div>
                    <div class="prompt-toggle-btn" id="toggle-${newTask.id}">
                        <span id="toggle-text-${newTask.id}">展开</span>
                        <i class="fas fa-chevron-down"></i>
                    </div>
                </div>
                <div class="task-prompt-content collapsed" id="prompt-${newTask.id}">
                    ${promptWithBreaks}
                </div>
            </div>
            
            ${newTask.taskStatus === 'Processing' || newTask.taskStatus === 'Pending' ? `
                <div class="task-progress-indicator">
                    <div class="progress-content">
                        <div class="progress-spinner"></div>
                        <div class="progress-info">
                            <div class="progress-top">
                                <span class="progress-message">${newTask.progressMessage || '处理中...'}</span>
                                <span class="progress-percent">${newTask.progress || 0}%</span>
                            </div>
                            <div class="progress-bar-container">
                                <div class="progress-bar" style="width: ${newTask.progress || 0}%;"></div>
                            </div>
                        </div>
                    </div>
                </div>
            ` : ''}
            
            ${newTask.resultImageUrl ? `
                <div class="task-image-container">
                    <img src="${newTask.thumbnailUrl || newTask.resultImageUrl}" 
                         alt="生成结果" 
                         class="task-image" 
                         onclick="showImageModal('${newTask.resultImageUrl}')">
                    <!-- 浮动拆分重绘按钮 -->
                    <button class="floating-split-btn" 
                            onclick="event.stopPropagation(); openSplitUpscaleModal(${newTask.id}, '${newTask.resultImageUrl}')"
                            title="拆分高清重绘">
                        <i class="fas fa-th"></i>
                        <span>拆分重绘</span>
                    </button>
                </div>
            ` : ''}
            
            ${newTask.errorMessage ? `
                <div class="task-error">
                    <i class="fas fa-exclamation-circle"></i>
                    <div class="error-content">
                        <strong>错误详情：</strong>
                        <span>${escapeHtml(newTask.errorMessage)}</span>
                    </div>
                </div>
            ` : ''}
            
            <!-- 优化后的操作按钮 -->
            ${newTask.resultImageUrl ? `
                <div class="task-actions-modern">
                    <button class="action-btn action-btn-copy" data-prompt="${escapeHtml(newTask.prompt)}" onclick="event.stopPropagation(); copyPromptFromData(this)">
                        <i class="fas fa-copy"></i>
                        <span>复制提示词</span>
                    </button>
                    <button class="action-btn action-btn-download" onclick="event.stopPropagation(); downloadImage('${newTask.resultImageUrl}')">
                        <i class="fas fa-download"></i>
                        <span>下载原图</span>
                    </button>
                    <button class="action-btn action-btn-view" onclick="event.stopPropagation(); showImageModal('${newTask.resultImageUrl}')">
                        <i class="fas fa-eye"></i>
                        <span>查看原图</span>
                    </button>
                </div>
            ` : `
                <div class="task-actions-modern">
                    <button class="action-btn action-btn-copy" data-prompt="${escapeHtml(newTask.prompt)}" onclick="event.stopPropagation(); copyPromptFromData(this)">
                        <i class="fas fa-copy"></i>
                        <span>复制提示词</span>
                    </button>
                    ${newTask.taskStatus === 'Processing' || newTask.taskStatus === 'Pending' ? `
                        <button class="action-btn action-btn-refresh" onclick="event.stopPropagation(); checkSingleTaskStatus(${newTask.id})">
                            <i class="fas fa-sync-alt"></i>
                            <span>检查状态</span>
                        </button>
                    ` : ''}
                </div>
            `}
            
            <div class="task-meta">
                <span class="meta-item">
                    <i class="fas fa-tag"></i>
                    <span>任务ID: ${newTask.id}</span>
                </span>
                <span class="meta-item">
                    <i class="fas fa-clock"></i>
                    <span>${newTask.completedAt ? '完成于 ' + formatTime(newTask.completedAt) : '处理中...'}</span>
                </span>
            </div>
        </div>
    `;

    if (insertBefore) {
        insertBefore.before(taskHtml);
    } else {
        taskList.append(taskHtml);
    }
}

// 更新现有任务项的内容
function updateExistingTaskItem(taskElement, task) {
    console.log(`🔄 更新任务 ${task.id}:`, {
        status: task.taskStatus,
        hasImage: !!task.resultImageUrl,
        hasError: !!task.errorMessage
    });

    // 更新任务头部的左侧信息
    const headerLeft = taskElement.find('.task-header-left');
    if (headerLeft.length > 0) {
        const canDelete = task.taskStatus === 'Completed' || task.taskStatus === 'Failed';

        headerLeft.html(`
            <span class="task-status ${task.taskStatus.toLowerCase()}">${getStatusText(task.taskStatus)}</span>
            <span class="task-mode">${getModeText(task.taskMode)}</span>
            <span class="task-id-inline">任务ID: ${task.id}</span>
            <span class="task-time-inline">${formatTime(task.createdAt)}</span>
        `);

        // 更新或添加删除按钮
        const deleteBtn = taskElement.find('.btn-delete-task');
        if (canDelete && deleteBtn.length === 0) {
            taskElement.find('.task-header').append(`
                <button class="btn-delete-task" onclick="event.stopPropagation(); deleteTask(${task.id})" title="删除任务">
                    <i class="fas fa-trash-alt"></i>
                </button>
            `);
        } else if (!canDelete && deleteBtn.length > 0) {
            deleteBtn.remove();
        }
    }

    // 🔧 修复：根据任务状态同步更新进度指示器
    if (task.taskStatus === 'Processing' || task.taskStatus === 'Pending') {
        let progressIndicator = taskElement.find('.task-progress-indicator');

        if (progressIndicator.length === 0) {
            // 创建进度指示器
            const progressHtml = `
                <div class="task-progress-indicator" style="margin: var(--spacing-sm) 0; padding: var(--spacing-sm); background: rgba(59, 130, 246, 0.1); border-radius: var(--radius-sm); border-left: 3px solid #3b82f6;">
                    <div style="display: flex; align-items: center; gap: var(--spacing-sm);">
                        <div class="progress-spinner" style="width: 16px; height: 16px; border: 2px solid #e5e7eb; border-top: 2px solid #3b82f6; border-radius: 50%; animation: spin 1s linear infinite;"></div>
                        <div style="flex: 1;">
                            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px;">
                                <div style="font-size: 0.875rem; font-weight: 500; color: #3b82f6;">
                                    ${task.progressMessage || (task.taskStatus === 'Pending' ? '等待处理中...' : 'AI正在生成中...')}
                                </div>
                                <div style="font-size: 0.75rem; font-weight: 600; color: #3b82f6;">
                                    ${task.progress || 0}%
                                </div>
                            </div>
                            <div style="width: 100%; height: 6px; background: #e5e7eb; border-radius: 3px; overflow: hidden;">
                                <div style="width: ${task.progress || 0}%; height: 100%; background: linear-gradient(90deg, #3b82f6, #60a5fa); border-radius: 3px; transition: width 0.3s ease-out;"></div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            taskElement.find('.task-prompt').after(progressHtml);
        } else {
            // 🔧 修复：更新现有进度指示器的状态文本
            const messageDiv = progressIndicator.find('div[style*="font-size: 0.875rem"]');
            const percentDiv = progressIndicator.find('div[style*="font-size: 0.75rem"]');
            const progressBar = progressIndicator.find('div[style*="width:"][style*="background: linear-gradient"]');

            // 根据任务状态显示正确的文本
            const statusMessage = task.progressMessage || (task.taskStatus === 'Pending' ? '等待处理中...' : 'AI正在生成中...');
            messageDiv.text(statusMessage);
            percentDiv.text(`${task.progress || 0}%`);
            progressBar.css('width', `${task.progress || 0}%`);

            fixPromptTextDirection();
        }
    } else {
        // 移除进度指示器（任务已完成或失败）
        taskElement.find('.task-progress-indicator').fadeOut(500, function () {
            $(this).remove();
        });
    }

    // 更新图片
    const imageContainer = taskElement.find('.task-image-container');
    if (task.resultImageUrl) {
        if (imageContainer.length === 0) {
            // 尝试多个插入点
            let insertPoint = taskElement.find('.task-prompt-box');
            if (insertPoint.length === 0) {
                insertPoint = taskElement.find('.task-prompt-content');
            }
            if (insertPoint.length === 0) {
                insertPoint = taskElement.find('.task-header');
            }

            if (insertPoint.length > 0) {
                insertPoint.after(`
                <div class="task-image-container">
                    <img src="${task.thumbnailUrl || task.resultImageUrl}" 
                         alt="生成结果" 
                         class="task-image" 
                         onclick="showImageModal('${task.resultImageUrl}')">
                </div>
            `);
            }
        } else {
            // 更新现有图片
            const img = imageContainer.find('.task-image');
            const newSrc = task.thumbnailUrl || task.resultImageUrl;
            if (img.attr('src') !== newSrc) {
                img.attr('src', newSrc);
                img.attr('onclick', `showImageModal('${task.resultImageUrl}')`);
            }
            
            // ✅ 确保浮动拆分重绘按钮存在
            if (imageContainer.find('.floating-split-btn').length === 0) {
                imageContainer.append(`
                    <button class="floating-split-btn" 
                            onclick="event.stopPropagation(); openSplitUpscaleModal(${task.id}, '${task.resultImageUrl}')"
                            title="拆分高清重绘">
                        <i class="fas fa-th"></i>
                        <span>拆分重绘</span>
                    </button>
                `);
            }
        }
        // 强制更新操作按钮
        const actionsContainer = taskElement.find('.task-actions-modern');
        if (actionsContainer.length > 0) {
            const newActionsHtml = `
            <div class="task-actions-modern">
                <button class="action-btn action-btn-copy" data-prompt="${escapeHtml(task.prompt)}" onclick="event.stopPropagation(); copyPromptFromData(this)">
                    <i class="fas fa-copy"></i>
                    <span>复制提示词</span>
                </button>
                <button class="action-btn action-btn-download" onclick="event.stopPropagation(); downloadImage('${task.resultImageUrl}')">
                    <i class="fas fa-download"></i>
                    <span>下载原图</span>
                </button>
                <button class="action-btn action-btn-view" onclick="event.stopPropagation(); showImageModal('${task.resultImageUrl}')">
                    <i class="fas fa-eye"></i>
                    <span>查看原图</span>
                </button>
            </div>
        `;
            actionsContainer.replaceWith(newActionsHtml);
        }
    }

    // 更新错误信息 - 显示更详细的错误信息
    const errorContainer = taskElement.find('.task-error');
    if (task.errorMessage) {
        const fullErrorHtml = `
        <div class="task-error">
            <i class="fas fa-exclamation-circle"></i>
            <div class="error-content">
                <strong>错误详情：</strong>
                <div style="margin-top: 4px; line-height: 1.4;">${escapeHtml(task.errorMessage)}</div>
            </div>
        </div>
    `;

        if (errorContainer.length === 0) {
            // 插入到图片容器后面或提示词后面
            const insertAfter = taskElement.find('.task-image-container').length > 0
                ? taskElement.find('.task-image-container')
                : taskElement.find('.task-prompt-box');
            insertAfter.after(fullErrorHtml);
        } else {
            errorContainer.replaceWith(fullErrorHtml);
        }
    } else {
        errorContainer.remove();
    }


    // 更新完成时间
    const timeElement = taskElement.find('.task-meta span:last');
    const newTimeText = task.completedAt ? '完成于: ' + formatTime(task.completedAt) : '处理中...';
    if (timeElement.text() !== newTimeText) {
        timeElement.text(newTimeText);
    }

    // 最后验证关键元素是否存在
    if (task.resultImageUrl && taskElement.find('.task-image-container').length === 0) {
        console.error(`❌ 任务 ${task.id} 有图片但未成功插入DOM`);
    }
    if (task.errorMessage && taskElement.find('.task-error').length === 0) {
        console.error(`❌ 任务 ${task.id} 有错误但未成功显示`);
    }
}

// 获取状态文本
function getStatusText(status) {
    const statusMap = {
        'Pending': '等待中',
        'Processing': '处理中',
        'Completed': '已完成',
        'Failed': '失败'
    };
    return statusMap[status] || status;
}

// 获取模式文本
function getModeText(mode) {
    const modeMap = {
        'generate': '图片生成',
        'localEdit': '局部编辑',
        'fusion': '图像融合',
        'textEdit': '文本编辑',
        'consistency': '一致性生成'
    };
    return modeMap[mode] || mode;
}

// 格式化时间
function formatTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diff = now - date;

    if (diff < 60000) return '刚刚';
    if (diff < 3600000) return Math.floor(diff / 60000) + '分钟前';
    if (diff < 86400000) return Math.floor(diff / 3600000) + '小时前';
    if (diff < 604800000) return Math.floor(diff / 86400000) + '天前';

    return date.toLocaleDateString('zh-CN');
}

// HTML转义
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// 刷新任务列表
async function refreshTaskList() {
    await loadTaskList(false); // 手动刷新时使用增量更新，保持用户当前位置
    checkAndStartPolling();
}

// 检查任务是否需要轮询（包括15分钟内完成的任务）
function shouldPollTask(task) {
    // 未完成的任务需要轮询
    if (task.taskStatus === 'Pending' || task.taskStatus === 'Processing') {
        return true;
    }

    // 已完成的任务在15分钟内也需要轮询
    if (task.taskStatus === 'Completed' && task.completedAt) {
        const completedTime = new Date(task.completedAt);
        const now = new Date();
        const timeDiff = now - completedTime;
        const fifteenMinutes = 10 * 60 * 1000; // 10分钟的毫秒数
        return timeDiff < fifteenMinutes;
    }

    return false;
}

// 检查并开启轮询
function checkAndStartPolling() {
    const hasPollingTasks = allTasks.some(task => shouldPollTask(task));



    if (hasPollingTasks) {
        startTaskPolling();
    } else {
        stopTaskPolling();
    }
}

// 开启任务轮询
function startTaskPolling() {
    // 如果已经在轮询，先停止
    stopTaskPolling();

    // 显示轮询指示器
    if (!$('.polling-indicator').length) {
        $('.card.fixed-height h2').append(`
            <div class="polling-indicator"  style="display:none;">
                <div class="polling-dot"></div>
                <span>监控中</span>
            </div>
        `);
    }

    // 每6秒查询一次
    taskPollingInterval = setInterval(async () => {
        // 检查是否有需要轮询的任务（包括15分钟内完成的任务）
        const hasPollingTasks = allTasks.some(task => shouldPollTask(task));

        if (hasPollingTasks) {
            await loadTaskList(false); // 轮询时保持用户滚动位置（使用增量更新）
        } else {
            // 没有需要轮询的任务了，停止轮询
            stopTaskPolling();
        }
    }, 6000);
}

// 停止任务轮询
function stopTaskPolling() {
    if (taskPollingInterval) {
        clearInterval(taskPollingInterval);
        taskPollingInterval = null;
    }
    $('.polling-indicator').remove();
}

// 检查单个任务状态
async function checkSingleTaskStatus(taskId) {
    try {
        const response = await fetch(`/api/tasks/${taskId}/status`, {
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        if (response.ok) {
            const status = await response.json();

            // 更新对应任务的状态
            const taskIndex = allTasks.findIndex(t => t.id === taskId);
            if (taskIndex !== -1) {
                allTasks[taskIndex].taskStatus = status.status;
                allTasks[taskIndex].resultImageUrl = status.resultImageUrl;
                allTasks[taskIndex].errorMessage = status.errorMessage;
                allTasks[taskIndex].completedAt = status.completedAt;
                renderTaskList(false, false); // 增量更新，保持用户滚动位置
            }
        }
    } catch (error) {

    }
}


// 删除任务
async function deleteTask(taskId) {
    if (!confirm('确定要删除这个任务吗？')) {
        return;
    }

    try {
        const response = await fetch(`/api/tasks/${taskId}`, {
            method: 'DELETE',
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        // 处理401 Unauthorized错误
        if (response.status === 401) {
            clearToken();
            showToast('登录已过期，请重新登录', 'error');
            setTimeout(() => {
                window.location.href = '/';
            }, 1000);
            return;
        }

        const result = await response.json();

        if (result.success) {
            showToast('任务删除成功', 'success');

            // 从本地数组中移除任务
            allTasks = allTasks.filter(t => t.id !== taskId);

            // 重新渲染任务列表
            renderTaskList(false, true);

            // 检查是否还有需要轮询的任务
            checkAndStartPolling();
        } else {
            showToast('删除失败: ' + result.message, 'error');
        }
    } catch (error) {
        console.error('删除任务失败:', error);
        showToast('删除失败: ' + error.message, 'error');
    }
}


// 显示图片模态框
function showImageModal(imageUrl) {
    const modal = document.getElementById('imageModal');
    const modalImg = document.getElementById('modalImage');

    modalImg.src = imageUrl;
    modal.style.display = 'flex';
    document.body.style.overflow = 'hidden'; // 防止背景滚动
}

// 关闭图片模态框
function closeImageModal() {
    const modal = document.getElementById('imageModal');
    modal.style.display = 'none';
    document.body.style.overflow = 'auto'; // 恢复背景滚动
}

// 下载模态框中的图片
function downloadModalImage() {
    const modalImg = document.getElementById('modalImage');
    const imageUrl = modalImg.src;

    // 使用原来的下载函数
    downloadImage(imageUrl);
}

// 点击模态框背景关闭
$(document).on('click', '#imageModal', function (e) {
    if (e.target === this) {
        closeImageModal();
    }
});

// ESC键关闭模态框
$(document).on('keydown', function (e) {
    if (e.key === 'Escape') {
        closeImageModal();
    }
});

// 从按钮的 data 属性复制提示词（避免转义问题）
async function copyPromptFromData(button) {
    try {
        const prompt = button.getAttribute('data-prompt');
        if (!prompt) {
            showToast('没有找到提示词内容', 'error');
            return;
        }

        // 解码 HTML 实体
        const textarea = document.createElement('textarea');
        textarea.innerHTML = prompt;
        const decodedPrompt = textarea.value;

        await navigator.clipboard.writeText(decodedPrompt);
        showCopySuccess();
    } catch (err) {

        // 备用方案
        fallbackCopy(button.getAttribute('data-prompt'));
    }
}

// 备用复制方案
function fallbackCopy(htmlEncodedText) {
    const textarea = document.createElement('textarea');
    textarea.innerHTML = htmlEncodedText;
    const text = textarea.value;

    const tempTextarea = document.createElement("textarea");
    tempTextarea.value = text;
    tempTextarea.style.position = "fixed";
    tempTextarea.style.left = "-999999px";
    document.body.appendChild(tempTextarea);
    tempTextarea.select();

    try {
        document.execCommand('copy');
        showCopySuccess();
    } catch (err) {
        alert('复制失败，请手动复制');
    }

    document.body.removeChild(tempTextarea);
}


// ==================== 任务进度显示功能 ====================

// 显示任务进度
function showTaskProgress(taskId, status) {
    const taskElement = $(`.task-item[data-task-id="${taskId}"]`);
    if (taskElement.length === 0) return;

    // 移除已有的进度指示器
    taskElement.find('.task-progress-indicator').remove();

    const progressHtml = `
        <div class="task-progress-indicator" style="margin: var(--spacing-sm) 0; padding: var(--spacing-sm); background: rgba(59, 130, 246, 0.1); border-radius: var(--radius-sm); border-left: 3px solid #3b82f6;">
            <div style="display: flex; align-items: center; gap: var(--spacing-sm);">
                <div class="progress-spinner" style="width: 16px; height: 16px; border: 2px solid #e5e7eb; border-top: 2px solid #3b82f6; border-radius: 50%; animation: spin 1s linear infinite;"></div>
                <div style="flex: 1;">
                    <div style="font-size: 0.875rem; font-weight: 500; color: #3b82f6;">
                        ${status === 'Pending' ? '等待处理中...' : 'AI正在生成中...'}
                    </div>
                    <div class="progress-message" style="font-size: 0.75rem; color: #6b7280; margin-top: 2px;">准备就绪</div>
                </div>
            </div>
        </div>
    `;

    taskElement.find('.task-header').after(progressHtml);

    // 开始动画进度消息
    if (status === 'Processing') {
        animateProgressMessage(taskId);
    }
}

// 更新任务进度
function updateTaskProgress(taskId, status) {
    const taskElement = $(`.task-item[data-task-id="${taskId}"]`);
    const progressIndicator = taskElement.find('.task-progress-indicator');

    if (progressIndicator.length > 0) {
        const messageDiv = progressIndicator.find('.progress-message');
        if (status === 'Processing') {
            messageDiv.text('AI正在生成中...');
            animateProgressMessage(taskId);
        } else if (status === 'Pending') {
            messageDiv.text('等待处理中...');
        }
    } else {
        showTaskProgress(taskId, status);
    }
}

// 隐藏任务进度
function hideTaskProgress(taskId, result) {
    const taskElement = $(`.task-item[data-task-id="${taskId}"]`);
    const progressIndicator = taskElement.find('.task-progress-indicator');

    if (progressIndicator.length > 0) {
        progressIndicator.fadeOut(500, function () {
            $(this).remove();
        });
    }
}

// 动画进度消息
function animateProgressMessage(taskId) {
    const messages = [
        'AI正在理解您的创意...',
        '深度学习网络正在工作...',
        '正在生成创意草图...',
        'AI正在精细化处理...',
        '正在进行细节渲染...',
        '即将完成，请稍候...'
    ];

    let messageIndex = 0;
    const interval = setInterval(() => {
        const taskElement = $(`.task-item[data-task-id="${taskId}"]`);
        const progressIndicator = taskElement.find('.task-progress-indicator');

        if (progressIndicator.length === 0) {
            clearInterval(interval);
            return;
        }

        const messageDiv = progressIndicator.find('.progress-message');
        if (messageDiv.length > 0) {
            messageDiv.text(messages[messageIndex]);
            messageIndex = (messageIndex + 1) % messages.length;
        }
    }, 3000);

    // 5秒后停止动画
    setTimeout(() => clearInterval(interval), 15000);
}

// 显示状态变化提示
function showStatusChangeToast(taskId, statusText, statusCode) {
    const messages = {
        'Completed': { text: '任务完成！', type: 'success' },
        'Failed': { text: '任务失败', type: 'error' },
        'Processing': { text: '开始处理', type: 'info' }
    };

    const message = messages[statusCode];
    if (message) {
        showToast(`任务 ${taskId} ${message.text}`, message.type);
    }
}

// 添加进度动画和提示词折叠样式
if (!$('#task-progress-styles').length) {
    $('head').append(`
        <style id="task-progress-styles">
        @keyframes spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
        }
        
        /* 提示词折叠样式 */
        .prompt-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            cursor: pointer;
            padding: 8px 12px;
            background: rgba(59, 130, 246, 0.05);
            border-radius: var(--radius-sm);
            border: 1px solid rgba(59, 130, 246, 0.2);
            margin-bottom: 8px;
            transition: all 0.2s ease;
        }
        
        .prompt-header:hover {
            background: rgba(59, 130, 246, 0.1);
            border-color: rgba(59, 130, 246, 0.3);
        }
        
        .prompt-header strong {
            color: var(--primary-color);
        }
        
        .prompt-toggle {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 0.875rem;
            color: var(--primary-color);
            transition: all 0.2s ease;
        }
        
        .prompt-toggle i {
            transition: transform 0.2s ease;
        }
        
        .prompt-toggle.expanded i {
            transform: rotate(180deg);
        }
        
        .prompt-content {
            background: var(--bg-secondary);
            padding: 12px;
            border-radius: var(--radius-sm);
            border: 1px solid var(--border-color);
            margin-top: 8px;
            line-height: 1.5;
            max-height: 300px;
            overflow-y: auto;
            word-wrap: break-word;
            text-align: left;  /* 从左往右对齐 */
            position: relative;
        }
        
        .prompt-content.collapsed {
            max-height: 80px;
            overflow: hidden;
            mask-image: linear-gradient(to bottom, black 70%, transparent 100%);
            -webkit-mask-image: linear-gradient(to bottom, black 70%, transparent 100%);
        }
        
        .prompt-content.collapsed::after {
            content: '';
            position: absolute;
            bottom: 0;
            left: 0;
            right: 0;
            height: 30px;
            background: linear-gradient(transparent, var(--bg-secondary));
            pointer-events: none;
        }
        </style>
    `);
}

// 切换提示词展开/收起状态
function togglePromptExpand(taskId) {
    // 🔧 修复：检测是否有文本被选中，如果有则不触发收缩
    const selection = window.getSelection();
    if (selection && selection.toString().length > 0) {

        return; // 有文本被选中时，不执行收缩操作
    }

    const promptContent = $(`#prompt-${taskId}`);
    const toggleBtn = $(`#toggle-${taskId}`);
    const toggleText = $(`#toggle-text-${taskId}`);
    const toggleIcon = $(`#toggle-${taskId} i`);

    if (promptContent.hasClass('collapsed')) {
        // 展开
        promptContent.removeClass('collapsed');
        toggleText.text('收起');
        toggleIcon.removeClass('fa-chevron-down').addClass('fa-chevron-up');
        toggleBtn.addClass('expanded');
    } else {
        // 收起
        promptContent.addClass('collapsed');
        toggleText.text('展开');
        toggleIcon.removeClass('fa-chevron-up').addClass('fa-chevron-down');
        toggleBtn.removeClass('expanded');
    }
}

// 确保所有提示词内容都有正确的文字方向
function fixPromptTextDirection() {
    $('.prompt-content').each(function () {
        $(this).css({
            'text-align': 'left',
            'direction': 'ltr',
            'unicode-bidi': 'plaintext'
        });
    });

    $('.gallery-prompt').each(function () {
        $(this).css({
            'text-align': 'left',
            'direction': 'ltr',
            'unicode-bidi': 'plaintext'
        });
    });
}

function fixHistoryPromptTextDirection() {
    $('.gallery-prompt').each(function () {
        $(this).css({
            'text-align': 'left',
            'direction': 'ltr',
            'unicode-bidi': 'plaintext'
        });
    });
}

async function loadHistory(page = 1) {
    try {
        const token = getToken();

        // 首先显示骨架屏加载效果
        renderLoadingSkeleton();
        $('#paginationContainer').hide();

        const response = await fetch(`/api/history?page=${page}`, {
            headers: {
                'Authorization': 'Bearer ' + token
            }
        });

        // 处理401 Unauthorized错误，跳转到登录页面
        if (response.status === 401) {
            clearToken();
            alert('登录已过期，请重新登录');
            window.location.href = '/';
            return;
        }

        // 如果被重定向到登录页面，也认为是认证失败
        if (response.url.includes('/Admin/Login')) {
            clearToken();
            alert('登录已过期，请重新登录');
            window.location.href = '/';
            return;
        }

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const result = await response.json();


        if (result.success) {
            currentPage = page;
            if (result.pagination) {
                totalPages = result.pagination.totalPages;
                totalCount = result.pagination.totalCount;
            }

            // 模拟快速渲染，给用户即时反馈
            setTimeout(() => {
                renderHistory(result.data);
                renderPagination(result.pagination);
            }, 100); // 极短的延迟，让用户看到加载状态

        } else {
            showError('加载历史记录失败: ' + result.message);
        }
    } catch (error) {

        showError('加载历史记录失败: ' + error.message);
    }
}

// 渲染加载骨架屏
function renderLoadingSkeleton() {
    const gallery = $('#historyGallery');
    gallery.empty();

    // 创建骨架屏项目
    const skeletonItems = [];
    for (let i = 0; i < 6; i++) {
        skeletonItems.push(`
            <div class="gallery-item skeleton-item">
                <div class="skeleton-image">
                    <div class="skeleton-shimmer"></div>
                </div>
                <div class="gallery-info">
                    <div class="skeleton-text skeleton-text-title"></div>
                    <div class="skeleton-text skeleton-text-meta"></div>
                    <div class="skeleton-text skeleton-text-meta"></div>
                    <div class="skeleton-buttons">
                        <div class="skeleton-button"></div>
                        <div class="skeleton-button"></div>
                    </div>
                </div>
            </div>
        `);
    }

    gallery.html(skeletonItems.join(''));
}

// 显示历史记录
function renderHistory(historyList) {
    const gallery = $('#historyGallery');
    gallery.empty();

    if (historyList.length === 0) {
        gallery.html(`
            <div style="text-align: center; width: 100%; padding: 40px; color: #666;">
                <i class="fas fa-images" style="font-size: 3rem; margin-bottom: 15px;"></i>
                <p>暂无历史记录</p>
            </div>
        `);
        return;
    }

    // 立即渲染所有卡片，使用简化的懒加载
    historyList.forEach((item, index) => {
        const imageUrl = item.imageUrl || '';
        const thumbnailUrl = item.thumbnailUrl || imageUrl; // 优先使用缩略图
        const promptRaw = item.prompt || '无描述'; // 原始提示词
        const prompt = escapeHtml(promptRaw); // HTML转义后的提示词
        const promptPreview = promptRaw.length > 100 ? promptRaw.substring(0, 100) + '...' : promptRaw;
        const promptWithBreaks = prompt.replace(/\n/g, '<br>');
        const modelName = item.modelName || '未知模型';
        const createdAt = item.createdAt || new Date();
        const id = item.id;

        // 使用与任务列表完全相同的HTML结构
        const cardHtml = `
            <div class="gallery-item">
                <div class="gallery-image-container">
                    <div class="image-loading-placeholder" id="placeholder-${id}">
                        <i class="fas fa-image" style="font-size: 2rem; margin-bottom: 8px; color: var(--primary-light);"></i>
                        <span style="font-size: 0.875rem; color: var(--text-secondary);">准备加载...</span>
                    </div>
                    <img class="gallery-image-lazy" 
                         data-src="${thumbnailUrl}" 
                         alt="生成的图片" 
                         style="cursor: pointer; display: none; width: 100%; height: 220px; object-fit: contain;"
                         onclick="showImageModal('${imageUrl}')"
                         id="img-${id}">
                    <!-- 浮动拆分重绘按钮 -->
                    <button class="floating-split-btn" 
                            onclick="event.stopPropagation(); openSplitUpscaleModal(${item.taskId || id}, '${imageUrl}')"
                            title="拆分高清重绘">
                        <i class="fas fa-th"></i>
                        <span>拆分重绘</span>
                    </button>
                </div>
                <div class="gallery-info">
                    <div class="task-prompt-box" onclick="toggleHistoryPromptExpand(${id})">
                        <div class="task-prompt-header">
                            <div class="prompt-preview">
                                <div class="prompt-label">
                                    <i class="fas fa-comment-dots"></i>
                                    <span class="label-text">提示词</span>
                                </div>
                                <span id="prompt-preview-${id}">${escapeHtml(promptPreview)}</span>
                            </div>
                            <div class="prompt-toggle-btn" id="history-toggle-${id}">
                                <span id="history-toggle-text-${id}">展开</span>
                                <i class="fas fa-chevron-down"></i>
                            </div>
                        </div>
                        <div class="task-prompt-content collapsed" id="history-prompt-${id}">
                            ${promptWithBreaks}
                        </div>
                    </div>
                    <div style="margin-top: 10px; font-size: 0.8rem; color: #999;">
                        <div><i class="fas fa-robot"></i> ${modelName}</div>
                        <div><i class="fas fa-clock"></i> ${formatDate(createdAt)}</div>
                    </div>
                    <div class="task-actions-modern">
                        <button class="action-btn action-btn-copy" data-prompt="${prompt}" onclick="event.stopPropagation(); copyPromptFromData(this)">
                            <i class="fas fa-copy"></i>
                            <span>复制提示词</span>
                        </button>
                        <button class="action-btn action-btn-download" onclick="event.stopPropagation(); downloadImage('${imageUrl}')">
                            <i class="fas fa-download"></i>
                            <span>下载原图</span>
                        </button>
                        <button class="action-btn action-btn-view" onclick="event.stopPropagation(); showImageModal('${imageUrl}')">
                            <i class="fas fa-eye"></i>
                            <span>查看原图</span>
                        </button>
                    </div>
                </div>
            </div>
        `;

        gallery.append(cardHtml);
    });

    // 直接加载所有图片（临时解决方案）
    setTimeout(() => {
        loadAllImages();
        // 修复历史记录提示词文字方向
        fixHistoryPromptTextDirection();
    }, 100);
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

// 查看图片 - 使用模态框
function viewImage(imageUrl) {
    showImageModal(imageUrl);
}

// 复制提示词到剪贴板
async function copyPrompt(prompt) {
    try {
        await navigator.clipboard.writeText(prompt);

        // 显示成功提示
        showCopySuccess();
    } catch (err) {


        // 备用方案：创建临时文本域
        const textArea = document.createElement("textarea");
        textArea.value = prompt;
        textArea.style.position = "fixed";
        textArea.style.left = "-999999px";
        textArea.style.top = "-999999px";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();

        try {
            document.execCommand('copy');
            showCopySuccess();
        } catch (err) {

            alert('复制失败，请手动复制提示词');
        }

        document.body.removeChild(textArea);
    }
}

// 显示复制成功提示
function showCopySuccess() {
    // 创建临时提示元素
    const toast = document.createElement('div');
    toast.innerHTML = '<i class="fas fa-check-circle"></i> 提示词已复制到剪贴板';
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: #28a745;
        color: white;
        padding: 12px 20px;
        border-radius: 6px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        z-index: 10000;
        font-size: 14px;
        animation: slideInRight 0.3s ease-out;
    `;

    document.body.appendChild(toast);

    // 3秒后自动消失
    setTimeout(() => {
        toast.style.animation = 'slideOutRight 0.3s ease-out';
        setTimeout(() => {
            if (document.body.contains(toast)) {
                document.body.removeChild(toast);
            }
        }, 300);
    }, 3000);
}

// 显示图片模态框
function showImageModal(imageUrl) {
    const modal = document.getElementById('imageModal');
    const modalImg = document.getElementById('modalImage');

    modalImg.src = imageUrl;
    modal.style.display = 'flex';
    document.body.style.overflow = 'hidden'; // 防止背景滚动
}

// 关闭图片模态框
function closeImageModal() {
    const modal = document.getElementById('imageModal');
    modal.style.display = 'none';
    document.body.style.overflow = 'auto'; // 恢复背景滚动
}

// 点击模态框背景关闭
$(document).on('click', '#imageModal', function (e) {
    if (e.target === this) {
        closeImageModal();
    }
});

// ESC键关闭模态框
$(document).on('keydown', function (e) {
    if (e.key === 'Escape') {
        closeImageModal();
    }
});

// 删除历史记录
async function deleteHistory(id) {
    if (!confirm('确定要删除这条记录吗?')) {
        return;
    }

    try {
        const response = await fetch(`/api/history/${id}`, {
            method: 'DELETE',
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        const result = await response.json();

        if (result.success) {
            // 如果当前页只有一条记录且不是第一页，则返回上一页
            if (totalCount === 1 && currentPage > 1) {
                currentPage--;
            }
            loadHistory(currentPage); // 重新加载当前页
        } else {
            alert('删除失败: ' + result.message);
        }
    } catch (error) {
        alert('删除失败: ' + error.message);
    }
}

// 渲染分页控件
function renderPagination(pagination) {
    const container = $('#paginationContainer');
    const paginationDiv = $('#pagination');
    const paginationInfo = $('#paginationInfo');

    if (!pagination || totalPages <= 1) {
        container.hide();
        return;
    }

    container.show();
    paginationDiv.empty();

    // 上一页按钮
    const prevBtn = `<button class="btn btn-secondary" onclick="loadHistory(${currentPage - 1})" ${!pagination.hasPreviousPage ? 'disabled' : ''}>
        <i class="fas fa-chevron-left"></i> 上一页
    </button>`;
    paginationDiv.append(prevBtn);

    // 页码按钮
    const startPage = Math.max(1, currentPage - 2);
    const endPage = Math.min(totalPages, startPage + 4);

    if (startPage > 1) {
        paginationDiv.append(`<button class="btn btn-secondary" onclick="loadHistory(1)">1</button>`);
        if (startPage > 2) {
            paginationDiv.append(`<span style="padding: 0 10px;">...</span>`);
        }
    }

    for (let i = startPage; i <= endPage; i++) {
        const activeClass = i === currentPage ? 'btn-primary' : 'btn-secondary';
        paginationDiv.append(`<button class="btn ${activeClass}" onclick="loadHistory(${i})">${i}</button>`);
    }

    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            paginationDiv.append(`<span style="padding: 0 10px;">...</span>`);
        }
        paginationDiv.append(`<button class="btn btn-secondary" onclick="loadHistory(${totalPages})">${totalPages}</button>`);
    }

    // 下一页按钮
    const nextBtn = `<button class="btn btn-secondary" onclick="loadHistory(${currentPage + 1})" ${!pagination.hasNextPage ? 'disabled' : ''}>
        下一页 <i class="fas fa-chevron-right"></i>
    </button>`;
    paginationDiv.append(nextBtn);

    // 分页信息
    paginationInfo.html(`共 ${totalCount} 条记录，第 ${currentPage}/${totalPages} 页`);
}

// 显示错误信息
function showError(message) {
    const gallery = $('#historyGallery');
    gallery.html(`
        <div style="text-align: center; width: 100%; padding: 40px; color: #dc3545;">
            <i class="fas fa-exclamation-triangle" style="font-size: 3rem; margin-bottom: 15px;"></i>
            <p>${message}</p>
            <button class="btn btn-primary" onclick="loadHistory(${currentPage})" style="margin-top: 15px;">
                <i class="fas fa-retry"></i> 重试
            </button>
        </div>
    `);
    $('#paginationContainer').hide();
}

// 初始化懒加载
function initLazyLoading() {


    const lazyImages = document.querySelectorAll('.gallery-image-lazy');


    if (lazyImages.length === 0) {

        return;
    }

    // 创建观察器用于实际加载图片
    const loadObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;
                const imageSrc = img.dataset.src;
                const imgId = img.id;
                const placeholderId = imgId.replace('img-', 'placeholder-');
                const placeholder = document.getElementById(placeholderId);



                if (imageSrc && imageSrc.trim() !== '') {
                    loadSingleImage(img, imageSrc, placeholder);
                    observer.unobserve(img);
                } else {
                    // 没有图片URL

                    if (placeholder) {
                        placeholder.innerHTML = `
                            <i class="fas fa-image" style="font-size: 2rem; margin-bottom: 8px; color: var(--text-muted);"></i>
                            <span style="font-size: 0.875rem; color: var(--text-muted);">无图片</span>
                        `;
                    }
                    observer.unobserve(img);
                }
            }
        });
    }, {
        rootMargin: '50px', // 提前50px开始加载
        threshold: 0.01 // 降低阈值，更容易触发
    });

    // 观察所有懒加载图片
    lazyImages.forEach((img, index) => {

        loadObserver.observe(img);
    });

    // 备用方案：1秒后直接加载可见的图片
    setTimeout(() => {

        lazyImages.forEach(img => {
            const rect = img.getBoundingClientRect();
            const isVisible = rect.top < window.innerHeight + 100 && rect.bottom > -100;

            if (isVisible) {
                const imageSrc = img.dataset.src;
                const imgId = img.id;
                const placeholderId = imgId.replace('img-', 'placeholder-');
                const placeholder = document.getElementById(placeholderId);


                if (imageSrc && imageSrc.trim() !== '') {
                    loadSingleImage(img, imageSrc, placeholder);
                }
            }
        });
    }, 1000);
}

// 加载单个图片的独立函数
function loadSingleImage(img, imageSrc, placeholder) {
    // 更新加载状态
    if (placeholder) {
        placeholder.innerHTML = `
            <i class="fas fa-spinner fa-spin" style="font-size: 1.5rem; margin-bottom: 8px; color: var(--primary-color);"></i>
            <span style="font-size: 0.875rem; color: var(--text-secondary);">正在加载图片...</span>
        `;
    }

    // 开始加载图片
    img.onload = function () {

        if (placeholder) {
            placeholder.style.display = 'none';
        }
        img.style.display = 'block';
    };

    img.onerror = function () {

        if (placeholder) {
            placeholder.innerHTML = `
                <i class="fas fa-exclamation-triangle" style="font-size: 1.5rem; margin-bottom: 8px; color: var(--error-color);"></i>
                <span style="font-size: 0.875rem; color: var(--error-color);">图片加载失败</span>
            `;
        }
    };

    img.src = imageSrc;
}

// 直接加载所有图片
function loadAllImages() {


    const lazyImages = document.querySelectorAll('.gallery-image-lazy');


    lazyImages.forEach((img, index) => {
        const imageSrc = img.dataset.src;
        const imgId = img.id;
        const placeholderId = imgId.replace('img-', 'placeholder-');
        const placeholder = document.getElementById(placeholderId);



        if (imageSrc && imageSrc.trim() !== '') {
            // 延迟加载，避免同时加载太多图片
            setTimeout(() => {
                loadSingleImage(img, imageSrc, placeholder);
            }, index * 200); // 每张图片延迟200ms
        } else {
            // 没有图片URL
            console.log('没有图片URL');
            if (placeholder) {
                placeholder.innerHTML = `
                    <i class="fas fa-image" style="font-size: 2rem; margin-bottom: 8px; color: var(--text-muted);"></i>
                    <span style="font-size: 0.875rem; color: var(--text-muted);">无图片</span>
                `;
            }
        }
    });
}



// 切换历史记录提示词展开/收起状态（与任务列表完全一致）
function toggleHistoryPromptExpand(historyId) {
    // 检测是否有文本被选中，如果有则不触发收缩
    const selection = window.getSelection();
    if (selection && selection.toString().length > 0) {
        return; // 有文本被选中时，不执行收缩操作
    }

    const promptContent = $(`#history-prompt-${historyId}`);
    const toggleBtn = $(`#history-toggle-${historyId}`);
    const toggleText = $(`#history-toggle-text-${historyId}`);
    const toggleIcon = $(`#history-toggle-${historyId} i`);

    if (promptContent.hasClass('collapsed')) {
        // 展开
        promptContent.removeClass('collapsed');
        toggleText.text('收起');
        toggleIcon.removeClass('fa-chevron-down').addClass('fa-chevron-up');
        toggleBtn.addClass('expanded');
    } else {
        // 收起
        promptContent.addClass('collapsed');
        toggleText.text('展开');
        toggleIcon.removeClass('fa-chevron-up').addClass('fa-chevron-down');
        toggleBtn.removeClass('expanded');
    }
}


// 清空历史记录
async function clearHistory() {
    if (!confirm('确定要清空所有历史记录吗?此操作不可恢复!')) {
        return;
    }

    try {
        const response = await fetch('/api/history/clear', {
            method: 'DELETE',
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        });

        const result = await response.json();

        if (result.success) {
            currentPage = 1; // 重置到第一页
            loadHistory(); // 重新加载列表
        } else {
            alert('清空失败: ' + result.message);
        }
    } catch (error) {
        alert('清空失败: ' + error.message);
    }
}

// 跳转到用户信息页面
function switchToProfile() {
    window.location.href = '/Home/Profile';
}

// ==================== 移动端回到顶部按钮功能 ====================
// 初始化回到顶部按钮
function initScrollToTopButton() {
    const scrollBtn = document.getElementById('scrollToTopBtn');
    
    if (!scrollBtn) return;
    
    // 监听窗口滚动事件
    window.addEventListener('scroll', function() {
        // 检查是否在移动端
        const isMobile = window.innerWidth <= 767;
        
        if (isMobile) {
            // 如果滚动距离超过300px，显示按钮
            if (window.pageYOffset > 300) {
                scrollBtn.classList.add('show');
            } else {
                scrollBtn.classList.remove('show');
            }
        } else {
            // 桌面端隐藏按钮
            scrollBtn.classList.remove('show');
        }
    });
}

// 滚动到顶部函数
function scrollToTop() {
    // 平滑滚动到顶部
    window.scrollTo({
        top: 0,
        behavior: 'smooth'
    });
}

// 页面加载完成后初始化回到顶部按钮
$(document).ready(function() {
    initScrollToTopButton();
});

// 监听窗口大小变化，响应式处理
window.addEventListener('resize', function() {
    const scrollBtn = document.getElementById('scrollToTopBtn');
    if (scrollBtn) {
        const isMobile = window.innerWidth <= 767;
        if (!isMobile) {
            scrollBtn.classList.remove('show');
        }
    }
});
