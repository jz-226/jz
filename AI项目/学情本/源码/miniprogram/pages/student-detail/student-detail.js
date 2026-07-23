// pages/student-detail/student-detail.js
const db = require('../../utils/db');
const util = require('../../utils/util');

Page({
  data: {
    studentId: '',
    student: {},
    stats: {},
    recentQuestions: [],
    recentLessons: [],
    avatarColor: '',
    initial: '',
    subjectsText: '',
    statusText: {
      'pending': '待讲解',
      'explained': '已讲解',
      'mastered': '已掌握'
    }
  },

  onLoad(options) {
    const studentId = options.id;
    this.setData({ studentId });
    this.loadStudent(studentId);
  },

  async loadStudent(studentId) {
    util.showLoading('加载中...');
    try {
      const student = await db.students.getById(studentId);
      const stats = await db.stats.getStudentStats(studentId);
      const recentQuestions = await db.questions.getByStudent(studentId);
      const recentLessons = await db.lessons.getByStudent(studentId);

      this.setData({
        student,
        stats,
        recentQuestions: recentQuestions.slice(0, 5),
        recentLessons: recentLessons.slice(0, 5),
        avatarColor: util.getAvatarColor(student.name),
        initial: util.getNameInitial(student.name),
        subjectsText: (student.subjects || []).join(' · ')
      });
    } catch (error) {
      console.error('加载学生详情失败:', error);
      util.showError('加载失败');
    } finally {
      util.hideLoading();
    }
  },

  onPhoneTap() {
    const phone = this.data.student.phone;
    if (phone) {
      wx.makePhoneCall({ phoneNumber: phone });
    }
  },

  onViewQuestions() {
    util.navigateTo(`/pages/questions/questions?studentId=${this.data.studentId}`);
  },

  onViewLessons() {
    util.navigateTo(`/pages/lessons/lessons?studentId=${this.data.studentId}`);
  },

  onQuestionTap(e) {
    const id = e.currentTarget.dataset.id;
    util.navigateTo(`/pages/question-edit/question-edit?id=${id}`);
  },

  onLessonTap(e) {
    const id = e.currentTarget.dataset.id;
    util.navigateTo(`/pages/lesson-edit/lesson-edit?id=${id}`);
  },

  onEdit() {
    util.navigateTo(`/pages/student-edit/student-edit?id=${this.data.studentId}`);
  },

  async onDelete() {
    const confirmed = await util.showConfirm('确定要删除这个学生吗？所有相关数据都将被删除。');
    if (!confirmed) return;

    try {
      await db.students.delete(this.data.studentId);
      await util.showSuccess('已删除');
      util.navigateBack();
    } catch (error) {
      console.error('删除学生失败:', error);
      util.showError('删除失败');
    }
  }
});
