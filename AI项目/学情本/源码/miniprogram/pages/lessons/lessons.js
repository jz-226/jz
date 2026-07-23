// pages/lessons/lessons.js
const db = require('../../utils/db');
const util = require('../../utils/util');

Page({
  data: {
    lessons: [],
    students: [],
    filterOptions: [
      { value: 'all', label: '全部课程' },
      { value: 'pending', label: '待上课' },
      { value: 'completed', label: '已完成' }
    ],
    filterIndex: 0,
    selectedDate: ''
  },

  onLoad(options) {
    // 如果从学生详情页进入，筛选该学生
    if (options.studentId) {
      this.setData({ studentId: options.studentId });
    }
    // loadData 由 onShow 触发
  },

  onShow() {
    // 设置自定义 tabBar 选中状态
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().setData({ selected: 2 });
    }
    this.loadData();
  },

  onPullDownRefresh() {
    this.loadData().finally(() => wx.stopPullDownRefresh());
  },

  async loadData() {
    util.showLoading('加载中...');
    try {
      const students = await db.students.getAll();

      let lessons;
      const filterValue = this.data.filterOptions[this.data.filterIndex].value;

      if (this.data.studentId) {
        lessons = await db.lessons.getByStudent(this.data.studentId);
      } else if (this.data.selectedDate) {
        lessons = await db.lessons.getByDate(this.data.selectedDate);
      } else if (filterValue !== 'all') {
        lessons = await db.lessons.getByStatus(filterValue);
      } else {
        lessons = await db.lessons.getAll();
      }

      // 为课程添加学生姓名
      const lessonsWithStudent = lessons.map(lesson => {
        const student = students.find(s => s._id === lesson.studentId);
        return {
          ...lesson,
          studentName: student ? student.name : '未知'
        };
      });

      this.setData({
        lessons: lessonsWithStudent,
        students
      });
    } catch (error) {
      console.error('加载课程列表失败:', error);
      util.showError('加载失败，请下拉重试');
    } finally {
      util.hideLoading();
    }
  },

  onFilterChange(e) {
    this.setData({
      filterIndex: e.detail.value,
      selectedDate: ''
    });
    this.loadData();
  },

  onDateChange(e) {
    this.setData({
      selectedDate: e.detail.value,
      filterIndex: 0
    });
    this.loadData();
  },

  onLessonTap(e) {
    const lesson = e.detail.lesson;
    util.navigateTo(`/pages/lesson-edit/lesson-edit?id=${lesson._id}`);
  },

  onAdd() {
    util.navigateTo('/pages/lesson-edit/lesson-edit');
  }
});
