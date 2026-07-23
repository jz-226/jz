// pages/summary-edit/summary-edit.js
const db = require('../../utils/db');
const util = require('../../utils/util');

Page({
  data: {
    id: '',
    lessons: [],
    lessonIndex: 0,
    selectedLessonText: '',
    studentName: '',
    stars: [1, 2, 3, 4, 5],
    form: {
      lessonId: '',
      studentId: '',
      understanding: 3,
      focus: 3,
      completion: 3,
      achievements: '',
      improvements: '',
      nextPlan: ''
    }
  },

  onLoad(options) {
    if (options.lessonId) {
      this.setData({ 'form.lessonId': options.lessonId });
    }
    if (options.studentId) {
      this.setData({ 'form.studentId': options.studentId });
    }

    this.loadLessons();

    if (options.id) {
      this.setData({ id: options.id });
      this.loadSummary(options.id);
    }
  },

  async loadLessons() {
    util.showLoading('加载中...');
    try {
      const students = await db.students.getAll();
      let lessons;

      if (this.data.form.studentId) {
        lessons = await db.lessons.getByStudent(this.data.form.studentId);
      } else {
        lessons = await db.lessons.getAll();
      }

      // 为课程添加显示文本
      const lessonsWithText = lessons.map(lesson => {
        const student = students.find(s => s._id === lesson.studentId);
        return {
          ...lesson,
          displayText: `${student ? student.name : '未知'} - ${lesson.subject} - ${lesson.date}`
        };
      });

      this.setData({ lessons: lessonsWithText });

      // 设置预选课程
      if (this.data.form.lessonId) {
        const lessonIndex = lessons.findIndex(l => l._id === this.data.form.lessonId);
        if (lessonIndex >= 0) {
          const lesson = lessons[lessonIndex];
          const student = students.find(s => s._id === lesson.studentId);
          this.setData({
            lessonIndex,
            selectedLessonText: lessonsWithText[lessonIndex].displayText,
            studentName: student ? student.name : '',
            'form.studentId': lesson.studentId
          });
        }
      }
    } catch (error) {
      console.error('加载课程列表失败:', error);
      util.showError('加载失败');
    } finally {
      util.hideLoading();
    }
  },

  async loadSummary(id) {
    util.showLoading('加载中...');
    try {
      const summary = await db.summaries.getById(id);
      const students = await db.students.getAll();
      const lesson = await db.lessons.getById(summary.lessonId);
      const student = students.find(s => s._id === summary.studentId);

      const lessonIndex = this.data.lessons.findIndex(l => l._id === summary.lessonId);

      this.setData({
        form: {
          lessonId: summary.lessonId,
          studentId: summary.studentId,
          understanding: summary.understanding || 3,
          focus: summary.focus || 3,
          completion: summary.completion || 3,
          achievements: summary.achievements || '',
          improvements: summary.improvements || '',
          nextPlan: summary.nextPlan || ''
        },
        lessonIndex: lessonIndex >= 0 ? lessonIndex : 0,
        selectedLessonText: lessonIndex >= 0 ? this.data.lessons[lessonIndex].displayText : '',
        studentName: student ? student.name : ''
      });
    } catch (error) {
      console.error('加载总结失败:', error);
      util.showError('加载失败');
    } finally {
      util.hideLoading();
    }
  },

  onLessonChange(e) {
    const index = e.detail.value;
    const lesson = this.data.lessons[index];

    this.setData({
      lessonIndex: index,
      selectedLessonText: lesson.displayText,
      'form.lessonId': lesson._id,
      'form.studentId': lesson.studentId,
      studentName: lesson.displayText.split(' - ')[0]
    });
  },

  onScoreTap(e) {
    const type = e.currentTarget.dataset.type;
    const value = e.currentTarget.dataset.value;
    this.setData({ [`form.${type}`]: value });
  },

  onAchievementsInput(e) {
    this.setData({ 'form.achievements': e.detail.value });
  },

  onImprovementsInput(e) {
    this.setData({ 'form.improvements': e.detail.value });
  },

  onNextPlanInput(e) {
    this.setData({ 'form.nextPlan': e.detail.value });
  },

  validate() {
    if (!this.data.form.lessonId) {
      util.showToast('请选择关联课程');
      return false;
    }
    return true;
  },

  async onSubmit() {
    if (!this.validate()) return;

    util.showLoading('保存中...');
    try {
      const data = {
        lessonId: this.data.form.lessonId,
        studentId: this.data.form.studentId,
        understanding: this.data.form.understanding,
        focus: this.data.form.focus,
        completion: this.data.form.completion,
        achievements: this.data.form.achievements.trim(),
        improvements: this.data.form.improvements.trim(),
        nextPlan: this.data.form.nextPlan.trim()
      };

      if (this.data.id) {
        await db.summaries.update(this.data.id, data);
      } else {
        await db.summaries.add(data);
      }

      await util.showSuccess('保存成功');
      util.navigateBack();
    } catch (error) {
      console.error('保存总结失败:', error);
      util.showError('保存失败');
    } finally {
      util.hideLoading();
    }
  },

  async onDelete() {
    const confirmed = await util.showConfirm('确定要删除这个总结吗？');
    if (!confirmed) return;

    try {
      await db.summaries.delete(this.data.id);
      await util.showSuccess('已删除');
      util.navigateBack();
    } catch (error) {
      console.error('删除总结失败:', error);
      util.showError('删除失败');
    }
  }
});
