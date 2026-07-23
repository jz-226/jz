// pages/summaries/summaries.js
const db = require('../../utils/db');
const util = require('../../utils/util');

Page({
  data: {
    summaries: [],
    students: [],
    stars: [1, 2, 3, 4, 5]
  },

  onLoad() {
    // 初始化，loadData 由 onShow 触发
  },

  onShow() {
    this.loadData();
  },

  onPullDownRefresh() {
    this.loadData().finally(() => wx.stopPullDownRefresh());
  },

  async loadData() {
    util.showLoading('加载中...');
    try {
      const students = await db.students.getAll();
      const summaries = await db.summaries.getAll();

      // 一次性读取所有 lessons，用 Map 索引（修复 O(n²) 问题）
      const allLessons = db.readCollection(db.STORAGE_KEYS.lessons);
      const lessonMap = new Map();
      if (Array.isArray(allLessons)) {
        allLessons.forEach(l => lessonMap.set(l._id, l));
      }

      // 为总结添加学生姓名和课程日期
      const summariesWithDetails = summaries.map(summary => {
        const student = students.find(s => s._id === summary.studentId);
        const lesson = lessonMap.get(summary.lessonId);
        return {
          ...summary,
          studentName: student ? student.name : '未知',
          lessonDate: lesson ? lesson.date : '未知',
          createTime: util.formatDateFriendly(util.formatDate(summary.createTime))
        };
      });

      this.setData({
        summaries: summariesWithDetails,
        students
      });
    } catch (error) {
      console.error('加载总结列表失败:', error);
      util.showError('加载失败，请下拉重试');
    } finally {
      util.hideLoading();
    }
  },

  onSummaryTap(e) {
    const id = e.currentTarget.dataset.id;
    util.navigateTo(`/pages/summary-edit/summary-edit?id=${id}`);
  },

  onAdd() {
    util.navigateTo('/pages/summary-edit/summary-edit');
  }
});
