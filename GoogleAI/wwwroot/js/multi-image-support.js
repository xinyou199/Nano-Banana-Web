// ==================== 多张图片支持模块 ====================
// 此文件提供前端多张图片展示和管理的辅助函数

/**
 * 解析 JSON 格式的图片URL数组
 * @param {string} imageUrls - JSON 格式的 URL 数组或单个 URL
 * @returns {Array} URL 数组
 */
function parseImageUrls(imageUrls) {
    if (!imageUrls) return [];
    
    try {
        // 如果是 JSON 数组字符串
        if (typeof imageUrls === 'string' && imageUrls.startsWith('[')) {
            return JSON.parse(imageUrls);
        }
        // 如果是单个 URL 字符串
        if (typeof imageUrls === 'string') {
            return [imageUrls];
        }
        // 如果已经是数组
        if (Array.isArray(imageUrls)) {
            return imageUrls;
        }
    } catch (e) {
        console.error('解析图片URL失败:', e);
        return [];
    }
    
    return [];
}

/**
 * 获取任务的所有图片 URL
 * 优先使用 ResultImageUrls，回退到 ResultImageUrl
 * @param {object} task - 任务对象
 * @returns {Array} 图片 URL 数组
 */
function getTaskImageUrls(task) {
    // 优先使用多图片字段
    if (task.resultImageUrls) {
        const urls = parseImageUrls(task.resultImageUrls);
        if (urls.length > 0) return urls;
    }
    
    // 回退到单图片字段
    if (task.resultImageUrl) {
        return [task.resultImageUrl];
    }
    
    return [];
}

/**
 * 获取历史记录的所有图片 URL
 * 优先使用 ImageUrls，回退到 ImageUrl
 * @param {object} history - 历史记录对象
 * @returns {Array} 图片 URL 数组
 */
function getHistoryImageUrls(history) {
    // 优先使用多图片字段
    if (history.imageUrls) {
        const urls = parseImageUrls(history.imageUrls);
        if (urls.length > 0) return urls;
    }
    
    // 回退到单图片字段
    if (history.imageUrl) {
        return [history.imageUrl];
    }
    
    return [];
}

/**
 * 生成多张图片的网格 HTML
 * @param {Array} imageUrls - 图片 URL 数组
 * @param {number} taskId - 任务 ID（用于点击处理）
 * @param {number} maxDisplay - 最多显示的图片数量（默认 3）
 * @returns {string} HTML 字符串
 */
function generateMultiImageGrid(imageUrls, taskId, maxDisplay = 3) {
    if (!imageUrls || imageUrls.length === 0) {
        return '';
    }
    
    const displayUrls = imageUrls.slice(0, maxDisplay);
    const hasMore = imageUrls.length > maxDisplay;
    
    let html = '<div class="multi-image-grid">';
    
    displayUrls.forEach((url, index) => {
        html += `
            <div class="image-grid-item" onclick="showImageCarousel(${taskId}, ${index})">
                <img src="${url}" alt="图片 ${index + 1}" class="grid-image" loading="lazy">
                <div class="image-index">${index + 1}/${imageUrls.length}</div>
            </div>
        `;
    });
    
    if (hasMore) {
        const remainCount = imageUrls.length - maxDisplay;
        html += `
            <div class="image-grid-item view-all" onclick="showImageCarousel(${taskId}, 0)">
                <div class="more-overlay">
                    <i class="fas fa-images"></i>
                    <span>查看全部</span>
                    <span class="count">+${remainCount}</span>
                </div>
            </div>
        `;
    }
    
    html += '</div>';
    return html;
}

/**
 * 显示图片轮播查看器
 * @param {number} taskId - 任务 ID
 * @param {number} startIndex - 开始显示的图片索引（默认 0）
 */
function showImageCarousel(taskId, startIndex = 0) {
    // 从任务列表中找到对应的任务
    const task = allTasks.find(t => t.id === taskId);
    if (!task) return;
    
    const imageUrls = getTaskImageUrls(task);
    if (imageUrls.length === 0) return;
    
    // 创建轮播模态框
    createImageCarouselModal(imageUrls, startIndex);
}

/**
 * 创建图片轮播模态框
 * @param {Array} imageUrls - 图片 URL 数组
 * @param {number} startIndex - 开始索引
 */
function createImageCarouselModal(imageUrls, startIndex = 0) {
    // 移除已存在的轮播模态框
    const existingModal = document.getElementById('imageCarouselModal');
    if (existingModal) {
        existingModal.remove();
    }
    
    let currentIndex = startIndex;
    
    const modalHtml = `
        <div id="imageCarouselModal" class="image-carousel-modal" onclick="closeImageCarousel(event)">
            <div class="carousel-content" onclick="event.stopPropagation()">
                <button class="carousel-nav carousel-prev" onclick="prevCarouselImage()"
                        ${currentIndex === 0 ? 'style="opacity: 0.5; cursor: not-allowed;"' : ''}>
                    <i class="fas fa-chevron-left"></i>
                </button>
                
                <div class="carousel-image-container">
                    <img id="carouselImage" src="${imageUrls[currentIndex]}" 
                         alt="图片 ${currentIndex + 1}" class="carousel-image">
                </div>
                
                <button class="carousel-nav carousel-next" onclick="nextCarouselImage()"
                        ${currentIndex === imageUrls.length - 1 ? 'style="opacity: 0.5; cursor: not-allowed;"' : ''}>
                    <i class="fas fa-chevron-right"></i>
                </button>
                
                <div class="carousel-info">
                    <div class="carousel-counter">
                        <span id="currentImageIndex">${currentIndex + 1}</span> / 
                        <span id="totalImages">${imageUrls.length}</span>
                    </div>
                    <button class="btn btn-secondary btn-sm" onclick="downloadImage('${imageUrls[currentIndex]}')">
                        <i class="fas fa-download"></i> 下载此图
                    </button>
                </div>
                
                <button class="close-carousel" onclick="closeImageCarousel()">
                    <i class="fas fa-times"></i>
                </button>
            </div>
        </div>
    `;
    
    document.body.insertAdjacentHTML('beforeend', modalHtml);
    
    // 保存当前轮播状态到全局变量供导航函数使用
    window.currentCarousel = {
        imageUrls: imageUrls,
        currentIndex: currentIndex
    };
    
    // 添加键盘导航
    document.addEventListener('keydown', handleCarouselKeyboard);
}

/**
 * 关闭图片轮播模态框
 */
function closeImageCarousel(event) {
    if (event && event.target.id !== 'imageCarouselModal') {
        return;
    }
    
    const modal = document.getElementById('imageCarouselModal');
    if (modal) {
        modal.remove();
    }
    
    document.removeEventListener('keydown', handleCarouselKeyboard);
    window.currentCarousel = null;
}

/**
 * 上一张图片
 */
function prevCarouselImage() {
    if (!window.currentCarousel) return;
    
    if (window.currentCarousel.currentIndex > 0) {
        window.currentCarousel.currentIndex--;
        updateCarouselDisplay();
    }
}

/**
 * 下一张图片
 */
function nextCarouselImage() {
    if (!window.currentCarousel) return;
    
    if (window.currentCarousel.currentIndex < window.currentCarousel.imageUrls.length - 1) {
        window.currentCarousel.currentIndex++;
        updateCarouselDisplay();
    }
}

/**
 * 更新轮播显示
 */
function updateCarouselDisplay() {
    if (!window.currentCarousel) return;
    
    const { imageUrls, currentIndex } = window.currentCarousel;
    const imageElement = document.getElementById('carouselImage');
    const indexElement = document.getElementById('currentImageIndex');
    
    if (imageElement) {
        imageElement.src = imageUrls[currentIndex];
    }
    
    if (indexElement) {
        indexElement.textContent = currentIndex + 1;
    }
    
    // 更新导航按钮状态
    const prevBtn = document.querySelector('.carousel-prev');
    const nextBtn = document.querySelector('.carousel-next');
    
    if (prevBtn) {
        if (currentIndex === 0) {
            prevBtn.style.opacity = '0.5';
            prevBtn.style.cursor = 'not-allowed';
        } else {
            prevBtn.style.opacity = '1';
            prevBtn.style.cursor = 'pointer';
        }
    }
    
    if (nextBtn) {
        if (currentIndex === imageUrls.length - 1) {
            nextBtn.style.opacity = '0.5';
            nextBtn.style.cursor = 'not-allowed';
        } else {
            nextBtn.style.opacity = '1';
            nextBtn.style.cursor = 'pointer';
        }
    }
}

/**
 * 处理轮播键盘导航
 */
function handleCarouselKeyboard(e) {
    if (e.key === 'ArrowLeft') {
        prevCarouselImage();
    } else if (e.key === 'ArrowRight') {
        nextCarouselImage();
    } else if (e.key === 'Escape') {
        closeImageCarousel();
    }
}

/**
 * 下载所有图片（打包成 zip）
 * 注：需要后端支持或前端库支持
 * @param {Array} imageUrls - 图片 URL 数组
 */
async function downloadAllImages(imageUrls) {
    if (!imageUrls || imageUrls.length === 0) {
        showToast('没有可下载的图片', 'warning');
        return;
    }
    
    if (imageUrls.length === 1) {
        // 单张图片直接下载
        downloadImage(imageUrls[0]);
        return;
    }
    
    // 多张图片逐个下载（带延迟防止浏览器限制）
    showToast(`正在下载 ${imageUrls.length} 张图片...`, 'info');
    
    for (let i = 0; i < imageUrls.length; i++) {
        await new Promise(resolve => {
            setTimeout(() => {
                downloadImage(imageUrls[i]);
                resolve();
            }, 500 * (i + 1));
        });
    }
}

// ==================== CSS 样式 ====================
// 将以下样式添加到 main.css 或直接在此处添加

if (!document.getElementById('multi-image-styles')) {
    const styleSheet = document.createElement('style');
    styleSheet.id = 'multi-image-styles';
    styleSheet.textContent = `
        /* 多张图片网格 */
        .multi-image-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 8px;
            margin: 12px 0;
        }
        
        .image-grid-item {
            position: relative;
            cursor: pointer;
            border-radius: var(--radius-md);
            overflow: hidden;
            border: 2px solid var(--border-color);
            transition: all 0.3s ease;
            background: var(--bg-secondary);
            aspect-ratio: 1;
        }
        
        .image-grid-item:hover {
            border-color: var(--primary-color);
            box-shadow: 0 4px 12px rgba(99, 102, 241, 0.2);
            transform: translateY(-2px);
        }
        
        .grid-image {
            width: 100%;
            height: 100%;
            object-fit: cover;
            display: block;
        }
        
        .image-index {
            position: absolute;
            top: 4px;
            right: 4px;
            background: rgba(0, 0, 0, 0.7);
            color: white;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 0.75rem;
            font-weight: 600;
        }
        
        .image-grid-item.view-all {
            background: linear-gradient(135deg, rgba(99, 102, 241, 0.1), rgba(168, 85, 247, 0.1));
            border: 2px dashed var(--primary-color);
        }
        
        .image-grid-item.view-all:hover {
            background: linear-gradient(135deg, rgba(99, 102, 241, 0.2), rgba(168, 85, 247, 0.2));
        }
        
        .more-overlay {
            width: 100%;
            height: 100%;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 4px;
            color: var(--primary-color);
            font-weight: 600;
        }
        
        .more-overlay i {
            font-size: 1.5rem;
        }
        
        .more-overlay .count {
            font-size: 0.875rem;
            opacity: 0.8;
        }
        
        /* 图片轮播模态框 */
        .image-carousel-modal {
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0, 0, 0, 0.95);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 9999;
            padding: 20px;
            animation: fadeIn 0.3s ease-out;
        }
        
        .carousel-content {
            position: relative;
            width: 100%;
            max-width: 900px;
            display: flex;
            align-items: center;
            gap: 20px;
        }
        
        .carousel-image-container {
            flex: 1;
            text-align: center;
        }
        
        .carousel-image {
            max-width: 100%;
            max-height: calc(100vh - 120px);
            border-radius: var(--radius-lg);
            object-fit: contain;
        }
        
        .carousel-nav {
            position: absolute;
            top: 50%;
            transform: translateY(-50%);
            background: rgba(255, 255, 255, 0.1);
            color: white;
            border: none;
            width: 48px;
            height: 48px;
            border-radius: 50%;
            cursor: pointer;
            font-size: 20px;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.3s ease;
        }
        
        .carousel-nav:hover:not([style*="opacity: 0.5"]) {
            background: rgba(255, 255, 255, 0.2);
        }
        
        .carousel-prev {
            left: -60px;
        }
        
        .carousel-next {
            right: -60px;
        }
        
        .carousel-info {
            position: absolute;
            bottom: -60px;
            left: 50%;
            transform: translateX(-50%);
            display: flex;
            align-items: center;
            gap: 20px;
            color: white;
        }
        
        .carousel-counter {
            font-weight: 600;
            font-size: 0.875rem;
        }
        
        .close-carousel {
            position: absolute;
            top: -50px;
            right: 0;
            background: rgba(255, 255, 255, 0.1);
            color: white;
            border: none;
            width: 40px;
            height: 40px;
            border-radius: 50%;
            cursor: pointer;
            font-size: 24px;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.3s ease;
        }
        
        .close-carousel:hover {
            background: rgba(255, 255, 255, 0.2);
        }
        
        /* 响应式设计 */
        @media (max-width: 768px) {
            .carousel-nav {
                width: 40px;
                height: 40px;
                font-size: 16px;
            }
            
            .carousel-prev {
                left: -45px;
            }
            
            .carousel-next {
                right: -45px;
            }
            
            .carousel-info {
                bottom: -50px;
            }
        }
        
        @keyframes fadeIn {
            from {
                opacity: 0;
            }
            to {
                opacity: 1;
            }
        }
    `;
    document.head.appendChild(styleSheet);
}
