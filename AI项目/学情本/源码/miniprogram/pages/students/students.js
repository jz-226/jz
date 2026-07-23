// pages/students/students.js
const db = require('../../utils/db');
const util = require('../../utils/util');

Page({
  data: {
    students: [],
    keyword: ''
  },

  onLoad() {
    // 初始化，loadData 由 onShow 触发
  },

  onShow() {
    // 设置自定义 tabBar 选中状态
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().setData({ selected: 1 });
    }
    this.loadStudents();
  },

  onPullDownRefresh() {
    this.loadStudents().finally(() => wx.stopPullDownRefresh());
  },

  async loadStudents() {
    util.showLoading('加载中...');
    try {
      const students = await db.students.getAll();

      // 获取每个学生的统计信息
      const studentsWithStats = await Promise.all(
        students.map(async (student) => {
          const stats = await db.stats.getStudentStats(student._id);
          return { ...student, stats };
        })
      );

      this.setData({ students: studentsWithStats });
    } catch (error) {
      console.error('加载学生列表失败:', error);
      util.showError('加载失败，请下拉重试');
    } finally {
      util.hideLoading();
    }
  },

  onSearch(e) {
    const keyword = e.detail.value.trim();
    this.setData({ keyword });

    // 300ms 防抖
    if (this._searchTimer) clearTimeout(this._searchTimer);
    this._searchTimer = setTimeout(() => {
      if (keyword) {
        this.searchStudents(keyword);
      } else {
        this.loadStudents();
      }
    }, 300);
  },

  async searchStudents(keyword) {
    try {
      const students = await db.students.search(keyword);
      this.setData({ students });
    } catch (error) {
      console.error('搜索失败:', error);
      util.showError('搜索失败');
    }
  },

  onStudentTap(e) {
    const student = e.detail.student;
    util.navigateTo(`/pages/student-detail/student-detail?id=${student._id}`);
  },

  onAdd() {
    util.navigateTo('/pages/student-edit/student-edit');
  }
});
