// app.js
App({
  onLaunch: function () {
    // 获取系统信息
    this.globalData.systemInfo = wx.getSystemInfoSync();
  },

  globalData: {
    systemInfo: null,
    userInfo: null,
    // 科目选项
    subjects: ['语文', '数学', '英语', '科学', '其他'],
    // 年级选项
    grades: ['一年级', '二年级', '三年级', '四年级', '五年级', '六年级'],
    // 问题状态
    questionStatus: [
      { value: 'pending', label: '待讲解', color: '#ff9800' },
      { value: 'explained', label: '已讲解', color: '#2196f3' },
      { value: 'mastered', label: '已掌握', color: '#4caf50' }
    ],
    // 课程状态
    lessonStatus: [
      { value: 'pending', label: '待上课', color: '#ff9800' },
      { value: 'completed', label: '已完成', color: '#4caf50' }
    ]
  }
});