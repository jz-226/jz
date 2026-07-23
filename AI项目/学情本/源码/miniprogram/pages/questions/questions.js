// pages/questions/questions.js
const db = require('../../utils/db');
const util = require('../../utils/util');
const app = getApp();

Page({
  data: {
    questions: [],
    students: [],
    studentId: '',
    statusFilters: [
      { value: 'all', label: '全部状态' },
      { value: 'pending', label: '待讲解' },
      { value: 'explained', label: '已讲解' },
      { value: 'mastered', label: '已掌握' }
    ],
    statusIndex: 0,
    subjectFilters: [
      { value: 'all', label: '全部科目' }
    ],
    subjectIndex: 0
  },

  onLoad(options) {
    // 从学生详情页进入时筛选该学生
    if (options.studentId) {
      this.setData({ studentId: options.studentId });
    }

    // 初始化科目筛选器
    this.setData({
      subjectFilters: [
        { value: 'all', label: '全部科目' },
        ...app.globalData.subjects.map(s => ({ value: s, label: s }))
      ]
    });

    // loadData 由 onShow 触发
  },

  onShow() {
    // 设置自定义 tabBar 选中状态
    if (typeof this.getTabBar === 'function' && this.getTabBar()) {
      this.getTabBar().setData({ selected: 3 });
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
      let questions;

      const statusValue = this.data.statusFilters[this.data.statusIndex].value;
      const subjectValue = this.data.subjectFilters[this.data.subjectIndex].value;

      if (this.data.studentId) {
        questions = await db.questions.getByStudent(this.data.studentId);
      } else if (statusValue !== 'all') {
        questions = await db.questions.getByStatus(statusValue);
      } else if (subjectValue !== 'all') {
        questions = await db.questions.getBySubject(subjectValue);
      } else {
        questions = await db.questions.getAll();
      }

      // 为问题添加学生姓名
      const questionsWithStudent = questions.map(q => {
        const student = students.find(s => s._id === q.studentId);
        return {
          ...q,
          studentName: student ? student.name : '未知'
        };
      });

      this.setData({
        questions: questionsWithStudent,
        students
      });
    } catch (error) {
      console.error('加载问题列表失败:', error);
      util.showError('加载失败，请下拉重试');
    } finally {
      util.hideLoading();
    }
  },

  onStatusChange(e) {
    this.setData({ statusIndex: e.detail.value });
    this.loadData();
  },

  onSubjectChange(e) {
    this.setData({ subjectIndex: e.detail.value });
    this.loadData();
  },

  onQuestionTap(e) {
    const question = e.detail.question;
    util.navigateTo(`/pages/question-edit/question-edit?id=${question._id}`);
  },

  onStatusChange2(e) {
    // 问题状态改变后刷新列表
    this.loadData();
  },

  onAdd() {
    if (this.data.studentId) {
      util.navigateTo(`/pages/question-edit/question-edit?studentId=${this.data.studentId}`);
    } else {
      util.navigateTo('/pages/question-edit/question-edit');
    }
  }
});
