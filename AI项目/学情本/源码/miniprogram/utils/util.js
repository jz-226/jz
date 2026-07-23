// utils/util.js - 通用工具函数

/**
 * 显示加载提示
 */
function showLoading(title = '加载中...') {
  wx.showLoading({
    title,
    mask: true
  });
}

/**
 * 隐藏加载提示
 */
function hideLoading() {
  wx.hideLoading();
}

/**
 * 显示成功提示（返回 Promise，resolve 时机为 toast 完成时）
 */
function showSuccess(title) {
  return new Promise((resolve) => {
    wx.showToast({
      title,
      icon: 'success',
      duration: 1500,
      complete: resolve
    });
  });
}

/**
 * 显示错误提示
 */
function showError(title) {
  wx.showToast({
    title,
    icon: 'error',
    duration: 2000
  });
}

/**
 * 显示普通提示
 */
function showToast(title) {
  wx.showToast({
    title,
    icon: 'none',
    duration: 2000
  });
}

/**
 * 显示确认对话框
 */
function showConfirm(content, title = '提示') {
  return new Promise((resolve) => {
    wx.showModal({
      title,
      content,
      success: (res) => {
        resolve(res.confirm);
      }
    });
  });
}

/**
 * 获取今天日期字符串
 */
function getToday() {
  const d = new Date();
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * 获取本周日期范围
 */
function getWeekRange() {
  const today = new Date();
  const dayOfWeek = today.getDay();
  const monday = new Date(today);
  monday.setDate(today.getDate() - (dayOfWeek === 0 ? 6 : dayOfWeek - 1));

  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);

  return {
    start: formatDate(monday),
    end: formatDate(sunday)
  };
}

/**
 * 格式化日期为友好格式
 */
function formatDateFriendly(dateStr) {
  const today = getToday();
  if (dateStr === today) {
    return '今天';
  }

  const d = new Date(dateStr);
  const todayDate = new Date(today);
  const diff = Math.floor((todayDate - d) / (1000 * 60 * 60 * 24));

  if (diff === 1) return '昨天';
  if (diff === 2) return '前天';
  if (diff <= 7) return `${diff}天前`;

  return dateStr;
}

/**
 * 格式化日期显示
 */
function formatDate(date) {
  const d = new Date(date);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * 生成头像背景色
 */
function getAvatarColor(name) {
  const colors = [
    '#4A90D9', '#5C6BC0', '#7E57C2', '#AB47BC',
    '#E91E63', '#EC407A', '#F44336', '#EF5350',
    '#FF7043', '#FF9800', '#FFCA28', '#66BB6A',
    '#43A047', '#26A69A', '#00ACC1', '#039BE5'
  ];
  const index = name.charCodeAt(0) % colors.length;
  return colors[index];
}

/**
 * 获取姓名首字
 */
function getNameInitial(name) {
  return name ? name.charAt(0) : '学';
}

/**
 * 安全解析JSON
 */
function copyText(text) {
  wx.setClipboardData({
    data: text,
    success: () => {
      showSuccess('已复制');
    }
  });
}

/**
 * 安全解析JSON
 */
function safeParse(str, defaultValue = null) {
  try {
    return JSON.parse(str);
  } catch (e) {
    return defaultValue;
  }
}

/**
 * 深拷贝
 */
function deepClone(obj) {
  return JSON.parse(JSON.stringify(obj));
}

/**
 * 数组去重
 */
function unique(arr) {
  return [...new Set(arr)];
}

/**
 * 页面跳转
 */
function navigateTo(url) {
  wx.navigateTo({ url });
}

/**
 * 页面重定向
 */
function redirectTo(url) {
  wx.redirectTo({ url });
}

/**
 * 返回上一页
 */
function navigateBack(delta = 1) {
  wx.navigateBack({ delta });
}

/**
 * 切换Tab页面
 */
function switchTab(url) {
  wx.switchTab({ url });
}

module.exports = {
  showLoading,
  hideLoading,
  showSuccess,
  showError,
  showToast,
  showConfirm,
  getToday,
  getWeekRange,
  formatDateFriendly,
  formatDate,
  getAvatarColor,
  getNameInitial,
  copyText,
  safeParse,
  deepClone,
  unique,
  navigateTo,
  redirectTo,
  navigateBack,
  switchTab
};